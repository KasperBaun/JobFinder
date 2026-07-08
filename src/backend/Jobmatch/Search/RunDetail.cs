using Jobmatch.Deduplication;
using Jobmatch.Jobs;

namespace Jobmatch.Search;

/// <summary>
/// Full detail for a single run — summary plus the shortlist and any marks.
/// The Raw / DedupeMerges / Scored / Dropped sections are optional and only
/// populated for runs produced after the T-006 transparency landed; older
/// run files leave them null.
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
    IReadOnlyDictionary<string, string> Marks,
    IReadOnlyList<ProviderRaw>? Raw = null,
    IReadOnlyList<DedupeGroup>? DedupeMerges = null,
    IReadOnlyList<ScoredEntry>? Scored = null,
    IReadOnlyList<DroppedEntry>? Dropped = null,
    JobSearchState? State = null,
    JobSearchPhase? Phase = null,
    IReadOnlyList<JobSearchEvent>? Timeline = null,
    IReadOnlyDictionary<string, string>? MarkReasons = null,
    IReadOnlyDictionary<string, string>? MarkStatuses = null);
