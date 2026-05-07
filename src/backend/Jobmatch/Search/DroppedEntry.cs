namespace Jobmatch.Search;

/// <summary>
/// One listing that didn't reach the shortlist, with the explicit reason.
/// Reason values: disqualifier, below_min_score, beyond_top_n, above_max_age,
/// missing_required_primary. Context carries a one-line specific (e.g.
/// 'score 0.18 below threshold 0.25').
/// </summary>
public sealed record DroppedEntry(
    string Id,
    string Title,
    string? Company,
    double Score,
    string Reason,
    string? Context);
