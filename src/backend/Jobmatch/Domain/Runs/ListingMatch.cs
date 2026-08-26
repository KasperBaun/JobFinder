using Jobmatch.Domain;

namespace Jobmatch.Domain.Runs;

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
    string? Description = null,
    // The LLM judge's verdict for the AI row on the card. Null when the judge didn't run and on
    // runs recorded before the fields existed (those carry it inside Reasoning as "AI review: …").
    double? LlmScore = null,
    string? LlmReason = null,
    // Other portals' copies of this ad, grouped into this slot by the probabilistic matcher
    // (R-117). Null when nothing was grouped and on runs recorded before the field existed.
    IReadOnlyList<ListingSighting>? Sightings = null);
