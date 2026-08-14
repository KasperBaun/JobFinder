namespace Jobmatch.Features.Providers;

/// <summary>
/// A source's fetch settings as they actually apply: the catalog defaults with any per-user
/// override layered on, plus which of them the user has tuned and what the shipped values were.
/// </summary>
public sealed record ProviderFetchSettings(
    string? Method,
    bool EnrichBody,
    bool Paginates,
    int? MaxPages,
    int? PageSize,
    /// <summary>The most listings this source can return in one run — max pages × page size.</summary>
    int? HardCeiling,
    string? SearchQuery,
    double RateLimitRps,
    ProviderFetchDefaults Defaults,
    bool RateLimitOverridden,
    bool EnrichBodyOverridden,
    bool MaxPagesOverridden,
    bool PageSizeOverridden);

/// <summary>The shipped values for the tunable knobs, so a caller can show what "reset" restores.</summary>
public sealed record ProviderFetchDefaults(
    int? MaxPages,
    int? PageSize,
    double RateLimitRps,
    bool EnrichBody);

/// <summary>Resolves what a source's fetch settings come to once the user's overrides are applied.</summary>
public static class ProviderFetchSettingsResolver
{
    // A source's search terms live in its query string under whichever name that platform uses.
    // Reported so the user can see what a source is actually asked for without reading its config.
    private static readonly string[] QueryKeys = ["q", "query", "keywords", "search", "searchText"];

    public static ProviderFetchSettings Resolve(PortalConfig portal, ProviderOverride? ov)
    {
        var pagination = portal.Pagination;
        var defaultMaxPages = pagination?.MaxPages;
        var defaultPageSize = pagination?.Size;
        var maxPages = ov?.MaxPages ?? defaultMaxPages;
        var pageSize = ov?.PageSize ?? defaultPageSize;

        return new ProviderFetchSettings(
            Method: portal.Method,
            EnrichBody: ov?.EnrichBody ?? portal.EnrichBody,
            Paginates: pagination is not null,
            MaxPages: maxPages,
            PageSize: pageSize,
            HardCeiling: maxPages is int mp && pageSize is int ps ? mp * ps : null,
            SearchQuery: SearchQueryOf(portal.QueryParams),
            RateLimitRps: ov?.RateLimitRps ?? portal.RateLimitRps,
            Defaults: new ProviderFetchDefaults(
                defaultMaxPages, defaultPageSize, portal.RateLimitRps, portal.EnrichBody),
            RateLimitOverridden: ov?.RateLimitRps is not null,
            EnrichBodyOverridden: ov?.EnrichBody is not null,
            MaxPagesOverridden: ov?.MaxPages is not null,
            PageSizeOverridden: ov?.PageSize is not null);
    }

    private static string? SearchQueryOf(IReadOnlyDictionary<string, object?>? queryParams)
    {
        if (queryParams is null) return null;

        foreach (var key in QueryKeys)
        {
            if (!queryParams.TryGetValue(key, out var value) || value is null) continue;
            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }
}
