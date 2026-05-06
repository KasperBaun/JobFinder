namespace Jobmatch.Gui.Server.Models;

public sealed record ProviderSummary(
    int Id,
    string Name,
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret);

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
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret,
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
