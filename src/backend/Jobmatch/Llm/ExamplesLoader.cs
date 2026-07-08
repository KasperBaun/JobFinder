using System.Text;
using YamlDotNet.Serialization;

namespace Jobmatch.Llm;

// User-curated archetype listings live as one markdown-with-frontmatter file per
// example under data/<email>/examples/. The frontmatter captures the structured
// signal (polarity, title, company, location, primary_stack, etc.); the body is
// a free-form "why this is a good/bad match" note that becomes the example's Note.
// Marked listings from previous runs also flow in as examples (MarkedExamplesLoader),
// carrying the user's mark reason as their Note.
//
// File naming convention: {polarity}-{slug}.md (polarity = "liked" | "disliked").
public sealed record ExampleListing(
    string Polarity,
    string Title,
    string Company,
    string? Location,
    string? Seniority,
    IReadOnlyList<string> PrimaryStack,
    IReadOnlyList<string> Domains,
    string? EmployerType,
    string? Note = null);

public static class ExamplesLoader
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder().Build();

    public static IReadOnlyList<ExampleListing> Load(string examplesDir)
    {
        if (!Directory.Exists(examplesDir)) return [];

        var examples = new List<ExampleListing>();
        foreach (var path in Directory.EnumerateFiles(examplesDir, "*.md"))
        {
            try
            {
                var ex = Parse(File.ReadAllText(path));
                if (ex is not null) examples.Add(ex);
            }
            catch { /* skip malformed files; we don't want a stray markdown to break a search */ }
        }
        return examples;
    }

    internal static ExampleListing? Parse(string fileContent)
    {
        // Frontmatter is the first YAML block delimited by `---` lines.
        if (!fileContent.StartsWith("---", StringComparison.Ordinal)) return null;
        var endIdx = fileContent.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIdx < 0) return null;
        var yaml = fileContent.Substring(3, endIdx - 3).Trim();

        var raw = Yaml.Deserialize<Dictionary<object, object?>>(yaml);
        if (raw is null) return null;

        var map = NormaliseKeys(raw);
        return new ExampleListing(
            Polarity: ReadString(map, "polarity") ?? "liked",
            Title: ReadString(map, "title") ?? string.Empty,
            Company: ReadString(map, "company") ?? string.Empty,
            Location: ReadString(map, "location"),
            Seniority: ReadString(map, "seniority"),
            PrimaryStack: ReadStringList(map, "primary_stack"),
            Domains: ReadStringList(map, "domains"),
            EmployerType: ReadString(map, "employer_type"),
            Note: ExtractNote(fileContent, endIdx));
    }

    // The prose after the frontmatter is the "why" — collapse it to one short line
    // so it fits the few-shot block without blowing the token budget.
    private const int MaxNoteLength = 240;

    private static string? ExtractNote(string fileContent, int frontmatterEndIdx)
    {
        var bodyStart = fileContent.IndexOf('\n', frontmatterEndIdx + 1);
        if (bodyStart < 0) return null;
        var body = fileContent.Substring(bodyStart + 1);
        var collapsed = string.Join(' ', body
            .Split('\n')
            .Select(l => l.TrimStart('#', ' ', '\t').Trim())
            .Where(l => l.Length > 0));
        if (collapsed.Length == 0) return null;
        return collapsed.Length <= MaxNoteLength ? collapsed : collapsed[..MaxNoteLength].TrimEnd() + "…";
    }

    private static IReadOnlyDictionary<string, object?> NormaliseKeys(IDictionary<object, object?> map)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in map)
        {
            var k = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(k)) result[k] = kvp.Value;
        }
        return result;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return null;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return [];
        if (v is IEnumerable<object?> seq)
            return seq.Select(o => o?.ToString()?.Trim() ?? string.Empty).Where(s => s.Length > 0).ToList();
        return [];
    }

    // Render the loaded examples as a compact text block suitable for the LLM
    // system prompt. Liked examples become positive few-shot, disliked become
    // explicit negatives. Capped at ~14 lines to keep tokens predictable;
    // examples carrying a "why" note win the cap — they're the strongest signal.
    public static string ToFewShotPrompt(IReadOnlyList<ExampleListing> examples)
    {
        if (examples.Count == 0) return "(no examples supplied; use the candidate profile alone)";

        var sb = new StringBuilder();
        var liked = SelectPolarity(examples, "liked", 8);
        var disliked = SelectPolarity(examples, "disliked", 6);

        if (liked.Count > 0)
        {
            sb.AppendLine("Examples of GOOD matches the candidate would take (target score ≈ 1.0):");
            foreach (var e in liked) sb.AppendLine(FormatExample(e));
        }
        if (disliked.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Examples of POOR matches the candidate would NOT take (target score ≈ 0.0):");
            foreach (var e in disliked) sb.AppendLine(FormatExample(e));
        }
        return sb.ToString().TrimEnd();
    }

    private static List<ExampleListing> SelectPolarity(IReadOnlyList<ExampleListing> examples, string polarity, int cap) =>
        examples
            .Where(e => string.Equals(e.Polarity, polarity, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Note is not null)
            .Take(cap)
            .ToList();

    private static string FormatExample(ExampleListing e)
    {
        var line = $"  - {e.Company} — {e.Title} ({e.Location ?? "?"}; {string.Join("/", e.PrimaryStack)})";
        return e.Note is null ? line : $"{line} — why: {e.Note}";
    }
}
