namespace Jobmatch.Features.Applications;

/// <summary>
/// One tracked application in the cross-run view: a statused listing resolved
/// from the newest run that carries a status for it (R-097).
/// </summary>
public sealed record ApplicationEntry(
    string ListingId,
    string RunId,
    DateTimeOffset RunStartedAt,
    ApplicationStatus Status,
    MarkKind? Mark,
    string? Reason,
    string Title,
    string? Company,
    string? Location,
    string Url,
    string Portal,
    string? PortalDisplayName,
    double Score,
    DateTimeOffset? StatusChangedAt = null);
