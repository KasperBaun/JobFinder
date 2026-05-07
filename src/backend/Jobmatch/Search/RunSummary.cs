namespace Jobmatch.Search;

/// <summary>
/// Compact summary of one search run shown in the history list.
/// </summary>
public sealed record RunSummary(
    string RunId,
    DateTimeOffset StartedAt,
    IReadOnlyList<ProviderRunStatus> Providers,
    int FetchedCount,
    int DedupedCount,
    int RankedCount,
    int ShortlistCount,
    double TopScore,
    int GoodMarks);
