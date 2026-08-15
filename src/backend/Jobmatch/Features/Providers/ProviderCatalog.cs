using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Features.Providers;

/// <summary>
/// Composes the shipped <c>portals.json</c>, the user's added sources
/// (<c>user-providers.json</c>) and their per-source state (<c>provider-state.json</c>) into one
/// provider list.
/// </summary>
/// <remarks>
/// This type exists because the composition used to be written out twice — once here and once inside
/// the search orchestrator — and the two copies disagreed: the search path read only the shipped
/// catalog, so a source the user added through the add-a-source flow (R-090) showed on the providers
/// page and could be tested, but was never fetched by an actual run.
/// </remarks>
public sealed class ProviderCatalog(UserContext ctx) : IProviderCatalog
{
    private static string ShippedCatalogPath() => Path.Combine(AppContext.BaseDirectory, "portals.json");

    public IReadOnlyList<PortalConfig> Shipped() => PortalCatalogLoader.Load(ShippedCatalogPath());

    public IReadOnlyList<PortalConfig> All()
    {
        var shipped = Shipped();
        var user = UserProviderStore.Load(ctx.UserProvidersPath);
        return user.Count == 0 ? shipped : [.. shipped, .. user];
    }

    public IReadOnlyList<PortalConfig> Effective() => ProviderStateMerger.Merge(All(), State());

    public ProviderState State() => ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
}
