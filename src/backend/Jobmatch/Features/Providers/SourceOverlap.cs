using System.Text.RegularExpressions;

namespace Jobmatch.Features.Providers;

/// <summary>
/// Pure set and string comparison behind the "you already have this" check. Kept free of I/O so the
/// thresholds can be tested without touching the network.
/// </summary>
public static partial class SourceOverlap
{
    /// <summary>At or above this share of the smaller set, the two sources are the same board.</summary>
    public const double DuplicateRatio = 0.9;

    /// <summary>Below this, the sources are unrelated enough that saying anything would be noise.</summary>
    public const double MentionRatio = 0.4;

    /// <summary>Comparing needs enough rows on both sides to mean anything.</summary>
    public const int MinComparableCount = 3;

    public static SourceOverlapMatch? Compare(
        int providerId,
        string displayName,
        IReadOnlyCollection<string> newUrls,
        IReadOnlyCollection<string> existingUrls)
    {
        if (newUrls.Count < MinComparableCount || existingUrls.Count < MinComparableCount) return null;

        var a = Normalize(newUrls);
        var b = Normalize(existingUrls);
        if (a.Count == 0 || b.Count == 0) return null;

        var smaller = Math.Min(a.Count, b.Count);
        a.IntersectWith(b);
        var shared = a.Count;
        var ratio = (double)shared / smaller;
        if (ratio < MentionRatio) return null;

        return new SourceOverlapMatch(
            ProviderId: providerId,
            DisplayName: displayName,
            ExistingCount: b.Count,
            SharedCount: shared,
            Ratio: ratio,
            Duplicate: ratio >= DuplicateRatio);
    }

    /// <summary>
    /// Same job, same board, two spellings: http vs https, a trailing slash, a tracking fragment, a
    /// capitalised host. Everything else — including the query string — is significant, because
    /// plenty of boards carry the job id there.
    /// </summary>
    public static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.Trim().TrimEnd('/').ToLowerInvariant();

        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Host.ToLowerInvariant()}{path.ToLowerInvariant()}{uri.Query}";
    }

    /// <summary>
    /// Token-set overlap, so "Danske Bank (Oracle)" and "Danske Bank" read as the same company while
    /// "Danske Spil" does not. One name containing the other scores full marks — "Danske Bank" and
    /// "Danske Bank Group" are one company — but a merely shared word is scored by Jaccard, which is
    /// what keeps the two Danske companies apart. Used only to pick which sources are worth fetching;
    /// the verdict always comes from comparing the jobs themselves.
    /// </summary>
    public static double NameSimilarity(string? a, string? b)
    {
        var left = Tokenize(a);
        var right = Tokenize(b);
        if (left.Count == 0 || right.Count == 0) return 0;

        var shared = left.Intersect(right, StringComparer.Ordinal).Count();
        if (shared == 0) return 0;
        if (shared == left.Count || shared == right.Count) return 1;

        return (double)shared / (left.Count + right.Count - shared);
    }

    private static HashSet<string> Normalize(IReadOnlyCollection<string> urls)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var url in urls)
        {
            if (!string.IsNullOrWhiteSpace(url)) set.Add(NormalizeUrl(url));
        }
        return set;
    }

    // Platform words are dropped: a catalog entry is named "Danske Bank (Oracle)" precisely because
    // it is on Oracle, so matching on that word would pair every Oracle board with every other one.
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "oracle", "greenhouse", "ashby", "lever", "smartrecruiters", "teamtailor",
        "workday", "feed", "rss", "jobs", "job", "careers", "career", "as", "a-s", "aps",
    };

    private static List<string> Tokenize(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? []
            : [.. TokenPattern().Split(s.ToLowerInvariant())
                .Where(t => t.Length > 1 && !Noise.Contains(t))
                .Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"[^a-z0-9æøå]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
