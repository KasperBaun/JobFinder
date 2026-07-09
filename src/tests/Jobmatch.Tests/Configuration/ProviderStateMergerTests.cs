using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderStateMergerTests
{
    private static PortalConfig Catalog(int id, string name, bool enabled = true, string? requiresSecret = null) =>
        new(Name: name, Type: PortalType.Manual, Enabled: enabled, Id: id, RequiresSecret: requiresSecret);

    private static ProviderState State(
        int[]? disabled = null,
        int[]? enabled = null,
        IReadOnlyDictionary<int, ProviderOverride>? overrides = null) =>
        new(disabled ?? Array.Empty<int>(),
            enabled ?? Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>(),
            overrides ?? new Dictionary<int, ProviderOverride>());

    private static PortalConfig Paginated(int id, string name) => new(
        Name: name, Type: PortalType.Rss, Enabled: true, Id: id,
        RateLimitRps: 0.5, EnrichBody: false,
        Pagination: new PaginationConfig(Param: "page", Size: 20, MaxPages: 8));

    [Fact]
    public void DisabledIdInState_OverridesEnabledTrue()
    {
        var catalog = new[] { Catalog(1, "a"), Catalog(2, "b") };
        var merged = ProviderStateMerger.Merge(catalog, State(disabled: new[] { 2 }));
        Assert.True(merged.First(p => p.Id == 1).Enabled);
        Assert.False(merged.First(p => p.Id == 2).Enabled);
    }

    [Fact]
    public void EnabledIdInState_OverridesCatalogDisabled()
    {
        // The toggle must be able to flip on a provider that ships off-by-default
        // in the catalog. Without this, the GUI toggle is silently a no-op.
        var catalog = new[] { Catalog(15, "it-jobbank-rss", enabled: false) };
        var merged = ProviderStateMerger.Merge(catalog, State(enabled: new[] { 15 }));
        Assert.True(merged[0].Enabled);
    }

    [Fact]
    public void EnabledOptInBeatsDisabledOptOut_WhenBothPresent()
    {
        // Pathological state: id appears in both lists. Treat opt-in as authoritative —
        // matches "I clicked enable last" intent.
        var catalog = new[] { Catalog(1, "a", enabled: false) };
        var merged = ProviderStateMerger.Merge(catalog, State(disabled: new[] { 1 }, enabled: new[] { 1 }));
        Assert.True(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_WithoutSecret_ProducesEffectivelyDisabled()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var merged = ProviderStateMerger.Merge(catalog, ProviderState.Empty);
        Assert.False(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_WithSecret_StaysEnabled()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var state = new ProviderState(
            Array.Empty<int>(),
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc" },
            },
            new Dictionary<int, ProviderOverride>());
        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.True(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_EmptyStringSecret_TreatedAsMissing()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var state = new ProviderState(
            Array.Empty<int>(),
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "" },
            },
            new Dictionary<int, ProviderOverride>());
        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.False(merged[0].Enabled);
    }

    [Fact]
    public void SecretsSubstitutedIntoQueryParams()
    {
        var catalog = new[]
        {
            new PortalConfig(
                Name: "jooble", Type: PortalType.Api, Enabled: true, Id: 5,
                RequiresSecret: "api_key",
                QueryParams: new Dictionary<string, object?> { ["api_key"] = "" }),
        };
        var state = new ProviderState(
            Array.Empty<int>(),
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "real-secret-value" },
            },
            new Dictionary<int, ProviderOverride>());

        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.Equal("real-secret-value", merged[0].QueryParams!["api_key"]?.ToString());
    }

    [Fact]
    public void Override_AppliesRateLimitEnrichAndPagination()
    {
        var catalog = new[] { Paginated(14, "jobindex-rss") };
        var overrides = new Dictionary<int, ProviderOverride>
        {
            [14] = new ProviderOverride(MaxPages: 20, PageSize: 50, RateLimitRps: 2.0, EnrichBody: true),
        };
        var merged = ProviderStateMerger.Merge(catalog, State(overrides: overrides));

        var p = merged[0];
        Assert.Equal(2.0, p.RateLimitRps);
        Assert.True(p.EnrichBody);
        Assert.Equal(20, p.Pagination!.MaxPages);
        Assert.Equal(50, p.Pagination!.Size);
    }

    [Fact]
    public void Override_PartialKeepsCatalogDefaultsForUnsetFields()
    {
        var catalog = new[] { Paginated(14, "jobindex-rss") };
        var overrides = new Dictionary<int, ProviderOverride> { [14] = new ProviderOverride(MaxPages: 3) };
        var merged = ProviderStateMerger.Merge(catalog, State(overrides: overrides));

        var p = merged[0];
        Assert.Equal(3, p.Pagination!.MaxPages);
        Assert.Equal(20, p.Pagination!.Size);   // catalog default kept
        Assert.Equal(0.5, p.RateLimitRps);      // catalog default kept
    }

    [Fact]
    public void Override_MaxPagesOnNonPaginatingSource_IsNoOp()
    {
        var catalog = new[] { Catalog(1, "pleo") };   // Manual/no pagination
        var overrides = new Dictionary<int, ProviderOverride> { [1] = new ProviderOverride(MaxPages: 10, PageSize: 99) };
        var merged = ProviderStateMerger.Merge(catalog, State(overrides: overrides));

        Assert.Null(merged[0].Pagination);   // still no pagination — the knob had nothing to apply to
    }

    [Fact]
    public void NoOverride_LeavesCatalogValuesUnchanged()
    {
        var catalog = new[] { Paginated(14, "jobindex-rss") };
        var merged = ProviderStateMerger.Merge(catalog, ProviderState.Empty);

        var p = merged[0];
        Assert.Equal(0.5, p.RateLimitRps);
        Assert.Equal(8, p.Pagination!.MaxPages);
        Assert.Equal(20, p.Pagination!.Size);
        Assert.False(p.EnrichBody);
    }
}
