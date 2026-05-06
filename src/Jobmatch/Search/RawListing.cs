namespace Jobmatch.Search;

/// <summary>
/// Slim wire-shape of a raw fetched listing — enough to identify and link
/// to the source, without dragging the full description through the
/// transparency UI. Description text lives only on the canonical scored
/// shortlist entries.
/// </summary>
public sealed record RawListing(
    string Id,
    string Title,
    string? Company,
    string? Location,
    string Url,
    DateTimeOffset? PostedAt);
