using Jobmatch.Jobs;

namespace Jobmatch.Search;

/// <summary>
/// Compact summary of one search run shown in the history list. <see cref="State"/>/<see cref="Phase"/>
/// reflect the run's lifecycle (queued / running / succeeded / failed / cancelled / interrupted); they
/// are null for legacy runs recorded before the background-job model and are read as succeeded.
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
    int GoodMarks,
    JobSearchState? State = null,
    JobSearchPhase? Phase = null);
