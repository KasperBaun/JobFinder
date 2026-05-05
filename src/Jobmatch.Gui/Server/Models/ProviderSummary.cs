namespace Jobmatch.Gui.Server.Models;

public sealed record ProviderSummary(
    string Name,
    string Type,
    bool Enabled,
    string? BaseUrl,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount);

public sealed record ProvidersResponse(IReadOnlyList<ProviderSummary> Providers);
