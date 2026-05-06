using Jobmatch.Models;

namespace Jobmatch.Search;

/// <summary>
/// One post-dedupe listing with its full score and per-component breakdown.
/// Includes every scored listing — not just the shortlist — so the user can
/// audit the ranking algorithm.
/// </summary>
public sealed record ScoredEntry(
    string Id,
    string Title,
    string? Company,
    string? Location,
    string Url,
    DateTimeOffset? PostedAt,
    string Portal,
    double Score,
    ScoreBreakdown Breakdown);
