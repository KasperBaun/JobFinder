namespace Jobmatch.Gui.Server.Models;

public sealed record ProviderSummary(
    string Name,
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount);

public sealed record ProvidersResponse(IReadOnlyList<ProviderSummary> Providers);

public sealed record ProviderUpsert(
    string? Name,
    string? Type,
    bool? Enabled,
    string? Endpoint,
    double? RateLimitRps,
    string? Notes);

public sealed record ProvidersUpdateRequest(IReadOnlyList<ProviderUpsert>? Providers);
