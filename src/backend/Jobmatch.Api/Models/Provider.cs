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
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret,
    bool Removable,
    IReadOnlyList<ProviderRecentRun> RecentRuns);

public sealed record ProviderUpsert(
    string? Name,
    string? Type,
    bool? Enabled,
    string? Endpoint,
    double? RateLimitRps,
    string? Notes);

public sealed record ProviderTestResult(
    bool Ok,
    int FetchedCount,
    long DurationMs,
    string? SampleTitle,
    string? Error,
    DateTimeOffset TestedAt);

public sealed record SetSecretsRequest(IReadOnlyDictionary<string, string> Values);

public sealed record DetectSourceRequest(string? Url);

public sealed record DetectedSourceDto(
    string Kind,
    string DisplayName,
    string Summary,
    string? DuplicateWarning);

public sealed record DetectSourceResponse(IReadOnlyList<DetectedSourceDto> Candidates);

public sealed record PreviewSourceRequest(string? Url, string? Kind, string? DisplayName);

public sealed record CreateSourceRequest(string? Url, string? Kind, string? DisplayName);

public sealed record ProviderCreatedResponse(int Id);
