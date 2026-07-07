using System.Text;
using System.Text.Json;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Llm;

// Turns raw CV text into an ExtractedProfile with one chat call, plus one
// retry when the reply doesn't parse. Same parsing posture as LlmJudge —
// strip fences, strict JSON first, then a loose {…} substring — but lenient
// per field: an invalid value nulls that field rather than failing the run.
public sealed class CvProfileExtractor(ILlmClient client, ILogger<CvProfileExtractor> logger)
{
    private static readonly string[] Seniorities = ["junior", "mid", "senior", "lead"];
    private static readonly string[] RemotePreferences = ["onsite", "hybrid", "remote"];

    public async Task<ExtractedProfile> ExtractAsync(string cvText, CancellationToken ct = default)
    {
        if (!await client.IsReachableAsync(ct))
            throw new InvalidRequestException("The AI model is not available — download it first.");

        var systemPrompt = BuildSystemPrompt();
        var raw = await client.ChatAsync(systemPrompt, BuildUserPrompt(cvText), ct);
        var profile = ParseProfile(raw);
        if (profile is not null) return profile;

        logger.LogWarning("CV extraction reply didn't parse; retrying once");
        raw = await client.ChatAsync(
            systemPrompt,
            BuildUserPrompt(cvText) + "\nReturn ONLY the JSON object — no explanation, no code fence.",
            ct);
        profile = ParseProfile(raw);
        if (profile is not null) return profile;

        logger.LogWarning("CV extraction failed after retry; raw reply: {Raw}", Truncate(raw));
        throw new InvalidRequestException(
            "The AI model could not extract a profile from that CV. Try pasting the CV text directly.");
    }

    internal static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You extract a candidate profile from CV/resume text. Output exactly one JSON object, nothing else — no code fence, no commentary. Schema:");
        sb.AppendLine("""{"name":string|null,"location":string|null,"country":string|null,"region":string|null,"metro":[string],"experienceYears":number|null,"seniority":"junior"|"mid"|"senior"|"lead"|null,"remotePreference":"onsite"|"hybrid"|"remote"|null,"targetRoles":[string],"primaryStack":[string],"secondaryStack":[string],"domains":[string],"languages":[string],"employmentTypes":[string]}""");
        sb.AppendLine("Rules:");
        sb.AppendLine("  - Only report what the CV states. Use null (or []) for anything not stated. Never invent.");
        sb.AppendLine("  - location = home city; country = home country; region = broader region if stated (e.g. Hovedstaden); metro = nearby cities the candidate could commute to, only if stated.");
        sb.AppendLine("  - experienceYears = total professional years; estimate from employment dates when not stated outright.");
        sb.AppendLine("  - seniority: infer from titles and years only if clear, else null.");
        sb.AppendLine("  - targetRoles = job titles the candidate holds or pursues (e.g. \"Backend Developer\").");
        sb.AppendLine("  - primaryStack = the 3-8 technologies most central to the candidate's work; secondaryStack = other technologies mentioned.");
        sb.AppendLine("  - domains = industries/problem areas worked in (e.g. fintech, logistics).");
        sb.AppendLine("  - languages = spoken/written human languages, NOT programming languages.");
        sb.AppendLine("  - employmentTypes = only if stated (e.g. full-time, freelance).");
        return sb.ToString();
    }

    internal static string BuildUserPrompt(string cvText) => "Extract the profile from this CV:\n" + cvText + "\n";

    internal static ExtractedProfile? ParseProfile(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var stripped = StripFences(raw.Trim());

        using var doc = TryParse(stripped) ?? TryParse(SubstringBraces(stripped));
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var root = doc.RootElement;
        return new ExtractedProfile(
            Name: ReadString(root, "name"),
            Location: ReadString(root, "location"),
            Country: ReadString(root, "country"),
            Region: ReadString(root, "region"),
            Metro: ReadList(root, "metro"),
            ExperienceYears: ReadYears(root, "experienceYears"),
            Seniority: ReadChoice(root, "seniority", Seniorities),
            RemotePreference: ReadChoice(root, "remotePreference", RemotePreferences),
            TargetRoles: ReadList(root, "targetRoles"),
            PrimaryStack: ReadList(root, "primaryStack"),
            SecondaryStack: ReadList(root, "secondaryStack"),
            Domains: ReadList(root, "domains"),
            Languages: ReadList(root, "languages"),
            EmploymentTypes: ReadList(root, "employmentTypes"));
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

    private static int? ReadYears(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
        && el.TryGetDouble(out var v) && v is >= 0 and <= 60
            ? (int)Math.Round(v)
            : null;

    private static string? ReadChoice(JsonElement root, string name, string[] allowed)
    {
        var value = ReadString(root, name)?.ToLowerInvariant();
        return value is not null && allowed.Contains(value) ? value : null;
    }

    private static IReadOnlyList<string> ReadList(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return [];
        if (el.ValueKind == JsonValueKind.String)
            return string.IsNullOrWhiteSpace(el.GetString()) ? [] : [el.GetString()!.Trim()];
        if (el.ValueKind != JsonValueKind.Array) return [];
        return el.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!.Trim())
            .ToList();
    }

    private static string Truncate(string text) => text.Length <= 500 ? text : text[..500] + "…";
}
