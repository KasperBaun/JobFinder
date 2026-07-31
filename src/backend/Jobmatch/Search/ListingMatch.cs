using Jobmatch.Models;

namespace Jobmatch.Search;

/// <summary>
/// Wire-shape DTO of a single ranked match — the GUI shortlist entry.
/// </summary>
public sealed record ListingMatch(
    string Id,
    string Portal,
    string Title,
    string? Company,
    string? Location,
    string RemoteMode,
    string Url,
    DateTimeOffset? PostedAt,
    double Score,
    string Reasoning,
    IReadOnlyList<string> PrimaryStackHits,
    IReadOnlyList<string> SecondaryStackHits,
    string? PortalDisplayName = null,
    bool FavoriteCompany = false,
    // Structured rationale; Reasoning stays populated as the English fallback for older runs.
    IReadOnlyList<ReasoningNote>? ReasoningNotes = null,
    // Full fetched ad text, persisted per run so a listing can be saved as PDF after the source
    // pruned it. Null on runs recorded before the field existed.
    string? Description = null);
