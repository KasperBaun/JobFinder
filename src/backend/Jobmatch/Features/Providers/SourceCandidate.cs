namespace Jobmatch.Features.Providers;

/// <summary>
/// One recognised way to add a source. <see cref="Draft"/> is the server-built provider config and
/// is never sent to the client verbatim — the API projects only <see cref="Kind"/>,
/// <see cref="DisplayName"/> and <see cref="Summary"/>. Create/preview re-run detection and select
/// by <see cref="Kind"/>, so the client never hands the server a raw endpoint or field mapping.
/// </summary>
public sealed record SourceCandidate(
    string Kind,
    string DisplayName,
    string Summary,
    PortalConfig Draft);
