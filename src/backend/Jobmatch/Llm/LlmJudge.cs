using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Llm;

public sealed record LlmVerdict(double Score, string Reason);

// Sends one chat-completion call per match, asking the model for a 0-1 fit score
// and a brief reason. The prompt embeds the user's skillset summary and the
// curated examples (as positive / negative few-shot). Sequential — Ollama on a
// single GPU can do one call at a time anyway.
public sealed class LlmJudge(ILlmClient client, ILogger<LlmJudge> logger)
{
    private static readonly Regex JsonObject = new(@"\{[^}]*""score""\s*:\s*([0-9.]+)[^}]*""reason""\s*:\s*""([^""]*)""[^}]*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public async Task<IReadOnlyList<(Match Match, LlmVerdict? Verdict)>> JudgeAsync(
        IReadOnlyList<Match> matches,
        Skillset skillset,
        IReadOnlyList<ExampleListing> examples,
        CancellationToken ct = default)
    {
        if (matches.Count == 0) return [];

        if (!await client.IsReachableAsync(ct))
        {
            logger.LogWarning("Ollama unreachable; skipping LLM judging — keyword-only ranking will stand");
            return matches.Select(m => (m, (LlmVerdict?)null)).ToList();
        }

        var systemPrompt = BuildSystemPrompt(skillset, examples);
        var results = new List<(Match, LlmVerdict?)>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            try
            {
                var userPrompt = BuildUserPrompt(m.Listing);
                var raw = await client.ChatAsync(systemPrompt, userPrompt, ct);
                var verdict = ParseVerdict(raw);
                if (verdict is null) logger.LogDebug("LLM returned unparseable response for {Title}: {Raw}", m.Listing.Title, raw);
                results.Add((m, verdict));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LLM judge failed for {Title} — falling back to keyword score", m.Listing.Title);
                results.Add((m, null));
            }
        }
        return results;
    }

    internal static string BuildSystemPrompt(Skillset skillset, IReadOnlyList<ExampleListing> examples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You evaluate how well a job listing matches a specific candidate. Output one JSON object on one line: {\"score\":0.0-1.0,\"reason\":\"<=15 word explanation\"}. Nothing else.");
        sb.AppendLine();
        sb.AppendLine("Candidate profile:");
        sb.AppendLine($"  Name: {skillset.Name}, {skillset.ExperienceYears} years experience, self-classifies as {skillset.Seniority}");
        sb.AppendLine($"  Location: {skillset.Location} (region {skillset.Region ?? "n/a"}), prefers {skillset.RemotePreference} work");
        if (skillset.PrimaryStack.Count > 0)
            sb.AppendLine($"  Primary stack (must-have): {string.Join(", ", skillset.PrimaryStack)}");
        if (skillset.SecondaryStack.Count > 0)
            sb.AppendLine($"  Secondary stack (nice-to-have): {string.Join(", ", skillset.SecondaryStack)}");
        if (skillset.TargetRoles.Count > 0)
            sb.AppendLine($"  Target roles: {string.Join(", ", skillset.TargetRoles)}");
        if (skillset.Domains.Count > 0)
            sb.AppendLine($"  Domains of interest: {string.Join(", ", skillset.Domains)}");
        if (skillset.Disqualifiers.Count > 0)
            sb.AppendLine($"  Hard disqualifiers (any of these → 0.0): {string.Join(", ", skillset.Disqualifiers)}");
        sb.AppendLine();
        sb.AppendLine(ExamplesLoader.ToFewShotPrompt(examples));
        sb.AppendLine();
        sb.AppendLine("Scoring guide:");
        sb.AppendLine("  1.0 — strong fit. Stack overlap, location fits, seniority matches, domain in target list.");
        sb.AppendLine("  0.7 — good fit, minor gap (e.g. one stack mismatch, slightly off seniority).");
        sb.AppendLine("  0.4 — weak fit (right region but wrong stack, or right stack but wrong role type).");
        sb.AppendLine("  0.1 — clear mismatch (non-engineering role, wrong region, junior/intern, etc.).");
        sb.AppendLine("  0.0 — disqualified.");
        return sb.ToString();
    }

    internal static string BuildUserPrompt(Listing listing)
    {
        var desc = (listing.Description ?? string.Empty);
        if (desc.Length > 1500) desc = desc.Substring(0, 1500) + " […truncated]";

        var sb = new StringBuilder();
        sb.AppendLine("Evaluate this listing:");
        sb.AppendLine($"  Title: {listing.Title}");
        sb.AppendLine($"  Company: {listing.Company ?? "?"}");
        sb.AppendLine($"  Location: {listing.Location ?? "?"}");
        sb.AppendLine($"  Remote: {listing.RemoteMode}");
        sb.AppendLine($"  Posted: {listing.PostedAt?.ToString("yyyy-MM-dd") ?? "?"}");
        sb.AppendLine($"  Description: {desc}");
        return sb.ToString();
    }

    internal static LlmVerdict? ParseVerdict(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Some models pad the JSON with a code fence or leading prose. Strip the obvious wrapper.
        var stripped = raw.Trim();
        if (stripped.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = stripped.IndexOf('\n');
            if (firstNl > 0) stripped = stripped.Substring(firstNl + 1);
            var fenceEnd = stripped.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd > 0) stripped = stripped.Substring(0, fenceEnd);
            stripped = stripped.Trim();
        }

        // Try strict JSON first.
        try
        {
            using var doc = JsonDocument.Parse(stripped);
            if (doc.RootElement.TryGetProperty("score", out var scoreEl) && scoreEl.TryGetDouble(out var sc))
            {
                var reason = doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString() ?? string.Empty
                    : string.Empty;
                return new LlmVerdict(Math.Clamp(sc, 0.0, 1.0), reason);
            }
        }
        catch { /* fall through to regex */ }

        // Loose fallback: pull score + reason out of an embedded JSON-looking object.
        var match = JsonObject.Match(stripped);
        if (match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var sc2))
        {
            return new LlmVerdict(Math.Clamp(sc2, 0.0, 1.0), match.Groups[2].Value);
        }
        return null;
    }
}
