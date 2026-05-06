using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderStateMergerTests
{
    private static PortalConfig Catalog(int id, string name, bool enabled = true, string? requiresSecret = null) =>
        new(Name: name, Type: PortalType.Manual, Enabled: enabled, Id: id, RequiresSecret: requiresSecret);

    [Fact]
    public void DisabledIdInState_OverridesEnabledTrue()
    {
        var catalog = new[] { Catalog(1, "a"), Catalog(2, "b") };
        var state = new ProviderState(new[] { 2 }, new Dictionary<int, IReadOnlyDictionary<string, string>>());

        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.True(merged.First(p => p.Id == 1).Enabled);
        Assert.False(merged.First(p => p.Id == 2).Enabled);
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
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc" },
            });
        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.True(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_EmptyStringSecret_TreatedAsMissing()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var state = new ProviderState(
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "" },
            });
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
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "real-secret-value" },
            });

        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.Equal("real-secret-value", merged[0].QueryParams!["api_key"]?.ToString());
    }
}
