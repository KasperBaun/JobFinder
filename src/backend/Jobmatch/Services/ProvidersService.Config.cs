using Jobmatch.Configuration;

namespace Jobmatch.Services;

// Per-user tuning of a source's fetch knobs (max pages, page size, rate limit, body enrichment),
// persisted into provider-state.json and layered over the catalog default by ProviderStateMerger.
public sealed partial class ProvidersService
{
    private const int MaxPagesCeiling = 50;
    private const int PageSizeCeiling = 200;
    private const double RateLimitCeiling = 10.0;

    public void SetConfigOverride(int id, ProviderOverride ov)
    {
        var catalog = LoadCatalog();
        var portal = catalog.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");

        var sanitized = Validate(portal, ov);

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var overrides = state.Overrides.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        if (sanitized.IsEmpty) overrides.Remove(id);   // empty => reset to catalog default
        else overrides[id] = sanitized;

        ProviderStateLoader.Save(ctx.ProviderStatePath, state with { Overrides = overrides });
    }

    private static ProviderOverride Validate(Models.PortalConfig portal, ProviderOverride ov)
    {
        if (ov.MaxPages is int mp && (mp <= 0 || mp > MaxPagesCeiling))
            throw new InvalidRequestException($"max pages must be between 1 and {MaxPagesCeiling}");
        if (ov.PageSize is int ps && (ps <= 0 || ps > PageSizeCeiling))
            throw new InvalidRequestException($"page size must be between 1 and {PageSizeCeiling}");
        if (ov.RateLimitRps is double rps && (rps <= 0 || rps > RateLimitCeiling))
            throw new InvalidRequestException($"rate limit must be between 0 and {RateLimitCeiling} requests/sec");

        // MaxPages/PageSize only mean anything for a paginating source; drop them otherwise so we don't
        // persist a knob that can never take effect (and so the UI doesn't show a phantom override).
        if (portal.Pagination is null)
            return ov with { MaxPages = null, PageSize = null };
        return ov;
    }
}
