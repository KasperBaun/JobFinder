using System.Net;

namespace Jobmatch.Search.Deduplication;

/// <summary>
/// Body-text comparison for the probabilistic matcher: word-shingle sets compared by
/// containment, so a portal's excerpt of an ad still registers against the full text. Shingle
/// hashes are process-local (never persisted) — both sides of a comparison are built in the
/// same run.
/// </summary>
internal static class DescriptionSimilarity
{
    private const int ShingleWords = 4;
    private const int MaxShingles = 2500;

    private static readonly IReadOnlySet<int> Empty = new HashSet<int>();

    internal static IReadOnlySet<int> Shingles(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return Empty;
        var text = description.Contains('&') ? WebUtility.HtmlDecode(description) : description;
        var words = Words(text);
        if (words.Count < ShingleWords) return Empty;

        var shingles = new HashSet<int>();
        for (var i = 0; i + ShingleWords <= words.Count && shingles.Count < MaxShingles; i++)
        {
            var hash = new HashCode();
            for (var j = 0; j < ShingleWords; j++) hash.Add(words[i + j]);
            shingles.Add(hash.ToHashCode());
        }
        return shingles;
    }

    /// <summary>|A∩B| / min(|A|,|B|) — 1.0 when the smaller text is wholly contained.</summary>
    internal static double Containment(IReadOnlySet<int> a, IReadOnlySet<int> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        var hits = small.Count(large.Contains);
        return (double)hits / small.Count;
    }

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                words.Add(text[start..i].ToLowerInvariant());
                start = -1;
            }
        }
        if (start >= 0) words.Add(text[start..].ToLowerInvariant());
        return words;
    }
}
