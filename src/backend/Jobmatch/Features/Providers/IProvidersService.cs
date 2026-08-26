namespace Jobmatch.Features.Providers;

public interface IProvidersService
{
    IReadOnlyList<ProviderListing> List();
    ProviderListingDetail GetById(int id);
    void SetEnabled(int id, bool enabled);
    void SetSecrets(int id, IReadOnlyDictionary<string, string> values);
    void SetConfigOverride(int id, ProviderOverride ov);
    Task<ProviderTestOutcome> TestAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<DetectedSource>> DetectAsync(string? url, CancellationToken ct);
    Task<SourcePreview> PreviewAsync(string? url, string kind, string? displayName, CancellationToken ct);
    Task<ProviderListing> CreateAsync(string? url, string kind, string? displayName, CancellationToken ct);
    void Delete(int id);
}
