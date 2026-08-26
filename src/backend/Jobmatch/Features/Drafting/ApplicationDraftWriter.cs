using System.Text;
using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Infrastructure.Llm;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Turns a CV, a skillset and one job ad into a tailored resume and cover letter with a single chat
/// call, plus one retry when the reply doesn't parse — the same posture as
/// <see cref="Jobmatch.Features.Cv.CvProfileExtractor"/>. Unlike extraction, a missing field here is
/// fatal: a draft without a resume is not a partial result, it is a failed one.
/// </summary>
public sealed class ApplicationDraftWriter(ILlmClient client, ILogger<ApplicationDraftWriter> logger)
{
    /// <summary>Ads run long; the tail is boilerplate about the company, so the head is the safe cut.</summary>
    public const int MaxAdChars = 6_000;

    /// <summary>
    /// CVs front-load identity, summary and skills, so the tail is the safe cut here too. Together with
    /// <see cref="MaxAdChars"/> and the reply budget this keeps the exchange inside the context the
    /// drafting service asks for.
    /// </summary>
    public const int MaxCvChars = 8_000;

    public async Task<ApplicationDraft> WriteAsync(DraftInputs inputs, CancellationToken ct = default)
    {
        if (!await client.IsReachableAsync(ct).ConfigureAwait(false))
            throw new InvalidRequestException("The AI model is not available — download it first.");

        var system = BuildSystemPrompt();
        var user = BuildUserPrompt(inputs);

        var raw = await client.ChatAsync(system, user, ct).ConfigureAwait(false);
        var draft = ParseDraft(raw);
        if (draft is not null) return draft;

        logger.LogWarning("Draft reply didn't parse; retrying once");
        raw = await client.ChatAsync(
            system,
            user + "\nReturn ONLY the JSON object — no explanation, no code fence.",
            ct).ConfigureAwait(false);
        draft = ParseDraft(raw);
        if (draft is not null) return draft;

        logger.LogWarning("Drafting failed after retry; raw reply: {Raw}", Truncate(raw, 500));
        throw new InvalidRequestException(
            "The AI model could not draft an application for that listing. Try again, or use a larger model.");
    }

    internal static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert resume writer. You write ATS-friendly, achievement-oriented resumes and tailored cover letters.");
        sb.AppendLine("You NEVER invent employers, job titles, dates, degrees, certifications or skills the candidate did not provide — you only select, reorder, and rephrase what the CV already states. If the candidate is a weak match, do the best you can with what is true rather than fabricating experience.");
        sb.AppendLine("Output exactly one JSON object, nothing else — no code fence, no commentary. Schema:");
        sb.AppendLine("""{"jobTitle":string,"companyName":string,"resumeMarkdown":string,"coverLetterMarkdown":string}""");
        sb.AppendLine("Rules:");
        sb.AppendLine("  - jobTitle and companyName are read off the job ad; use \"\" if the ad does not state one.");
        sb.AppendLine("  - resumeMarkdown: a full resume in Markdown (## headings, \"- \" bullets, **bold**). About one page. Lead with the experience the ad asks for.");
        sb.AppendLine("  - coverLetterMarkdown: 3-4 short paragraphs addressed to the hiring team, naming the role and company when the ad states them.");
        sb.AppendLine("  - Mirror the ad's terminology only where it is truthful of the candidate.");
        sb.AppendLine("  - Write both documents in the same language as the job ad.");
        return sb.ToString();
    }

    internal static string BuildUserPrompt(DraftInputs inputs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## CANDIDATE CV (the only source of facts)");
        sb.AppendLine(Truncate(inputs.CvText, MaxCvChars));
        sb.AppendLine();

        if (inputs.Skillset is not null)
        {
            sb.AppendLine("## CANDIDATE TARGETING (what they are aiming for — not facts to assert)");
            sb.AppendLine(DescribeSkillset(inputs.Skillset));
            sb.AppendLine();
        }

        sb.AppendLine("## JOB AD");
        sb.AppendLine(Truncate(inputs.JobAdText, MaxAdChars));
        sb.AppendLine();
        sb.AppendLine("## TASK");
        sb.AppendLine("Write a resume and a cover letter tailored to this job ad, using only the CV above as fact. Return ONLY the JSON object described in the system prompt.");
        return sb.ToString();
    }

    private static string DescribeSkillset(Skillset s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Name: {s.Name}");
        sb.AppendLine($"Location: {s.Location}");
        if (s.TargetRoles.Count > 0) sb.AppendLine($"Target roles: {string.Join(", ", s.TargetRoles)}");
        if (s.PrimaryStack.Count > 0) sb.AppendLine($"Primary stack: {string.Join(", ", s.PrimaryStack)}");
        if (s.SecondaryStack.Count > 0) sb.AppendLine($"Secondary stack: {string.Join(", ", s.SecondaryStack)}");
        if (s.Domains.Count > 0) sb.AppendLine($"Domains: {string.Join(", ", s.Domains)}");
        if (s.Languages.Count > 0) sb.AppendLine($"Languages: {string.Join(", ", s.Languages)}");
        return sb.ToString();
    }

    /// <summary>Null when the reply is unparseable or either document came back empty.</summary>
    internal static ApplicationDraft? ParseDraft(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var stripped = StripFences(raw.Trim());

        using var doc = TryParse(stripped) ?? TryParse(SubstringBraces(stripped));
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        var resume = ReadString(root, "resumeMarkdown");
        var coverLetter = ReadString(root, "coverLetterMarkdown");
        if (resume is null || coverLetter is null) return null;

        return new ApplicationDraft(
            JobTitle: ReadString(root, "jobTitle") ?? string.Empty,
            CompanyName: ReadString(root, "companyName") ?? string.Empty,
            ResumeMarkdown: resume,
            CoverLetterMarkdown: coverLetter);
    }

    internal static string StripFences(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var firstNl = text.IndexOf('\n');
        if (firstNl > 0) text = text[(firstNl + 1)..];
        var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceEnd > 0) text = text[..fenceEnd];
        return text.Trim();
    }

    private static JsonDocument? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonDocument.Parse(text); }
        catch (JsonException) { return null; }
    }

    private static string? SubstringBraces(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(el.GetString())
            ? el.GetString()!.Trim()
            : null;

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + " […truncated]";
}
