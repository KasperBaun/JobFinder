namespace Jobmatch.Api.Models;

public sealed record ProviderSummary(
    int Id,
    string Name,
    string DisplayName,
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    string? NotesDa,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret,
    bool Removable);

public sealed record ProvidersResponse(IReadOnlyList<ProviderSummary> Providers);

public sealed record ProviderRecentRun(
    string RunId,
    DateTimeOffset StartedAt,
    string Status,
    int? FetchedCount,
    string? Error);

public sealed record ProviderDetail(
    int Id,
    string Name,
    string DisplayName,
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    string? NotesDa,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret,
    bool Removable,
    IReadOnlyList<ProviderRecentRun> RecentRuns,
    ProviderConfigDto Config);

/// <summary>How a source is fetched — surfaced on the provider page so the user can see (and,
/// via the config-override endpoint, tune) the settings that govern how much it pulls back.
/// Effective values (defaults with any per-user override applied); <see cref="Defaults"/> plus the
/// Overridden flags let the UI show "default (N)" hints and mark tuned knobs.</summary>
public sealed record ProviderConfigDto(
    string? Method,
    bool EnrichBody,
    bool Paginates,
    int? MaxPages,
    int? PageSize,
    int? HardCeiling,
    string? SearchQuery,
    double RateLimitRps,
    ProviderConfigDefaults Defaults,
    bool RateLimitOverridden,
    bool EnrichBodyOverridden,
    bool MaxPagesOverridden,
    bool PageSizeOverridden);

/// <summary>The catalog (shipped) values for the tunable knobs, so the UI can show what "reset" restores.</summary>
public sealed record ProviderConfigDefaults(
    int? MaxPages,
    int? PageSize,
    double RateLimitRps,
    bool EnrichBody);

public sealed record ProviderUpsert(
    string? Name,
    string? Type,
    bool? Enabled,
    string? Endpoint,
    double? RateLimitRps,
    string? Notes);

/// <summary>Per-user override of a source's fetch knobs. Any null field = keep the catalog default;
/// all-null = reset the source to its defaults.</summary>
public sealed record ProviderConfigUpdate(
    int? MaxPages,
    int? PageSize,
    double? RateLimitRps,
    bool? EnrichBody);

public sealed record ProviderTestResult(
    bool Ok,
    int FetchedCount,
    long DurationMs,
    string? SampleTitle,
    string? Error,
    DateTimeOffset TestedAt,
    IReadOnlyList<ProviderTestSampleDto> Samples,
    bool HitPageCap,
    bool PossiblyCapped);

public sealed record ProviderTestSampleDto(
    string Title,
    string? Company,
    string? Location,
    string Url);

public sealed record SetSecretsRequest(IReadOnlyDictionary<string, string> Values);

public sealed record DetectSourceRequest(string? Url);

public sealed record DetectedSourceDto(
    string Kind,
    string DisplayName,
    string Summary);

public sealed record DetectSourceResponse(IReadOnlyList<DetectedSourceDto> Candidates);

public sealed record PreviewSourceRequest(string? Url, string? Kind, string? DisplayName);

/// <summary>A source the candidate turned out to duplicate. <see cref="Ratio"/> is the share of the
/// smaller job set the two have in common; <see cref="Duplicate"/> marks a 1-to-1 match.</summary>
public sealed record SourceOverlapDto(
    int ProviderId,
    string DisplayName,
    int ExistingCount,
    int SharedCount,
    double Ratio,
    bool Duplicate);

/// <summary>The preview a candidate earns by being run: what it fetched, and whether the user
/// already has a source returning the same jobs.</summary>
public sealed record SourcePreviewResult(ProviderTestResult Test, SourceOverlapDto? Overlap);

public sealed record CreateSourceRequest(string? Url, string? Kind, string? DisplayName);

public sealed record ProviderCreatedResponse(int Id);
