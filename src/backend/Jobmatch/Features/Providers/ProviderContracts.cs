using Jobmatch.Domain;

namespace Jobmatch.Features.Providers;

public sealed record ProviderListing(
    PortalConfig Portal,
    bool Enabled,
    bool HasSecret,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount);

public sealed record ProviderRunHistory(
    string RunId,
    DateTimeOffset StartedAt,
    string Status,
    int? FetchedCount,
    string? Error);

public sealed record ProviderListingDetail(
    ProviderListing Listing,
    IReadOnlyList<ProviderRunHistory> RecentRuns,
    ProviderOverride? Override = null);

public sealed record ProviderTestOutcome(
    bool Ok,
    int FetchedCount,
    long DurationMs,
    string? SampleTitle,
    string? Error,
    DateTimeOffset TestedAt,
    IReadOnlyList<ProviderTestSample> Samples,
    bool HitPageCap = false,
    bool PossiblyCapped = false);

/// <summary>A lightweight preview row from a provider test — enough to eyeball what a source returns,
/// without the full description payload. Capped to the first N listings of the fetch.</summary>
public sealed record ProviderTestSample(
    string Title,
    string? Company,
    string? Location,
    string Url);

public sealed record DetectedSource(
    string Kind,
    string DisplayName,
    string Summary);

/// <summary>What a candidate returned when it was actually run, plus — only if it returned
/// something — the source the user already has that it duplicates.</summary>
public sealed record SourcePreview(ProviderTestOutcome Test, SourceOverlapMatch? Overlap);
