namespace Jobmatch.Search;

/// <summary>
/// Full detail for a single run — summary plus the shortlist and any marks.
/// </summary>
public sealed record RunDetail(
    string RunId,
    DateTimeOffset StartedAt,
    IReadOnlyList<ProviderRunStatus> Providers,
    int FetchedCount,
    int DedupedCount,
    int RankedCount,
    int ShortlistCount,
    double TopScore,
    int GoodMarks,
    IReadOnlyList<ListingMatch> Shortlist,
    IReadOnlyDictionary<string, string> Marks);
