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

        var system = BuildSystemPrompt(inputs.Language);
        var user = BuildUserPrompt(inputs);

        var raw = await client.ChatAsync(system, user, ct).ConfigureAwait(false);
        var draft = ParseDraft(raw, inputs.JobTitle, inputs.CompanyName);
        if (draft is not null) return draft;

        logger.LogWarning("Draft reply didn't parse; retrying once");
        raw = await client.ChatAsync(
            system,
            user + "\nReturn ONLY the JSON object — no explanation, no code fence.",
            ct).ConfigureAwait(false);
        draft = ParseDraft(raw, inputs.JobTitle, inputs.CompanyName);
        if (draft is not null) return draft;

        logger.LogWarning("Drafting failed after retry; raw reply: {Raw}", Truncate(raw, 500));
        throw new InvalidRequestException(
            "The AI model could not draft an application for that listing. Try again, or use a larger model.");
    }

    internal static string BuildSystemPrompt(DraftLanguage language)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert resume writer. You write ATS-friendly, achievement-oriented resumes and tailored cover letters.");
        sb.AppendLine("You NEVER invent employers, job titles, dates, degrees, certifications or skills the candidate did not provide — you only select, reorder, and rephrase what the CV already states.");
        sb.AppendLine("This applies to the cover letter exactly as it does to the resume: do not claim experience with anything the ad asks for unless the CV names it.");
        sb.AppendLine("Concretely: NEVER name a technology, tool, platform or methodology in either document unless that exact name appears in the CV. If the ad asks for something the CV does not name, leave it out entirely rather than gesturing at it — writing \"my experience with X\" where the CV never mentions X is the single worst thing you can do here.");
        sb.AppendLine("If the candidate is a weak match, write LESS rather than filling the space — a short honest letter is worth more than a padded one.");
        sb.AppendLine($"Write both documents in {Name(language)}, including the section headings and the closing.");
        sb.AppendLine("Output exactly one JSON object, nothing else — no code fence, no commentary. Schema:");
        sb.AppendLine("""{"resumeMarkdown":string,"coverLetterMarkdown":string}""");
        sb.AppendLine();
        sb.AppendLine("resumeMarkdown — a full resume in Markdown, about one page:");
        sb.AppendLine("  - Start with the candidate's name and contact details from the CV.");
        sb.AppendLine("  - Use \"## \" for every section heading and \"### \" for each employer or degree. NEVER use bold text as a heading — \"**SUMMARY**\" is wrong, \"## SUMMARY\" is right. Use \"- \" for bullets and **bold** only for emphasis inside a line.");
        sb.AppendLine("  - Write the summary for THIS role in 2-3 lines. Do not copy the CV's own summary across unchanged.");
        sb.AppendLine("  - Keep every job and every date the CV lists. Within a job, put the bullets this ad cares about first, and keep each bullet's facts exactly as the CV states them.");
        sb.AppendLine();
        sb.AppendLine("coverLetterMarkdown — a short letter in plain text (no headings, no bullets), in exactly this order:");
        sb.AppendLine("  1. A greeting to the hiring team, with nothing before it — no letterhead, no address block, no date line. NEVER write a placeholder in square or angle brackets anywhere in the letter: if you do not know a person's name, greet the team.");
        sb.AppendLine("  2. Why this role at this company, referring to something the ad actually says. Open on a specific sentence: NEVER open with \"I am writing to express my interest\", \"I am excited to apply\" or any variation of either.");
        sb.AppendLine("  3. The two or three things in the CV this ad asks for, named concretely — the employer, the project, the number. If the CV gives you fewer than two genuinely relevant things, make this paragraph shorter rather than padding it out.");
        sb.AppendLine("  4. A brief close.");
        sb.AppendLine($"  5. The closing line \"{Closing(language)},\" on its own line, and on the line after it the candidate's real name as the CV spells it. Write the name itself, never a description of it. Every letter ends this way; a letter without it is incomplete.");
        sb.AppendLine();
        sb.AppendLine("Mirror the ad's terminology only where it is truthful of the candidate.");
        return sb.ToString();
    }

    private static string Name(DraftLanguage language) => language switch
    {
        DraftLanguage.Danish => "Danish",
        _ => "English",
    };

    private static string Closing(DraftLanguage language) => language switch
    {
        DraftLanguage.Danish => "Med venlig hilsen",
        _ => "Sincerely",
    };

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

        sb.AppendLine("## ROLE APPLIED FOR (from the listing record — authoritative)");
        sb.AppendLine($"Title: {inputs.JobTitle}");
        if (!string.IsNullOrWhiteSpace(inputs.CompanyName))
            sb.AppendLine($"Company: {inputs.CompanyName}");
        sb.AppendLine();

        sb.AppendLine("## JOB AD");
        sb.AppendLine(Truncate(inputs.JobAdText, MaxAdChars));
        sb.AppendLine();
        sb.AppendLine("## TASK");
        sb.AppendLine($"Write a resume and a cover letter tailored to this job ad, in {Name(inputs.Language)}, using only the CV above as fact. Return ONLY the JSON object described in the system prompt.");
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

    /// <summary>
    /// Null when the reply is unparseable or either document came back empty. The role and employer
    /// are supplied by the caller, not read from the reply: they are known facts about the listing,
    /// and asking a model to echo them only creates something for it to get wrong.
    /// </summary>
    internal static ApplicationDraft? ParseDraft(string raw, string jobTitle, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var stripped = StripFences(raw.Trim());

        using var doc = TryParse(stripped) ?? TryParse(SubstringBraces(stripped));
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        var resume = ReadString(root, "resumeMarkdown");
        var coverLetter = ReadString(root, "coverLetterMarkdown");
        if (resume is null || coverLetter is null) return null;

        coverLetter = StripPlaceholderLines(Clean(coverLetter));
        resume = Clean(resume);
        if (coverLetter.Length == 0 || resume.Length == 0) return null;

        return new ApplicationDraft(
            JobTitle: jobTitle,
            CompanyName: companyName ?? string.Empty,
            ResumeMarkdown: resume,
            CoverLetterMarkdown: coverLetter);
    }

    /// <summary>
    /// Drops any line that is nothing but a bracketed placeholder. The prompt forbids them, and after
    /// three phrasings it still produced one every few drafts — <c>[Date]</c> in a letterhead, or the
    /// shape of the closing echoed back as <c>&lt;the candidate's name&gt;</c>. A line like that is
    /// never content, and it is the one defect a user would be embarrassed to send.
    /// </summary>
    private static string Clean(string text) =>
        text.Replace(TruncationMarker, string.Empty, StringComparison.Ordinal).Trim();

    internal static string StripPlaceholderLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var kept = lines.Where(line =>
        {
            var t = line.Trim();
            return !((t.StartsWith('[') && t.EndsWith(']')) || (t.StartsWith('<') && t.EndsWith('>')))
                || t.Length <= 2;
        });

        return string.Join("\n", kept).Trim();
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

    /// <summary>Marks where an over-long input was cut. Stripped from the reply: it is our note to the model, not text to write down.</summary>
    internal const string TruncationMarker = "[…truncated]";

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + " " + TruncationMarker;
}
