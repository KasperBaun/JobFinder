using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderListValidatorTests
{
    private static PortalConfig Make(int id, string name, PortalType type, string? endpoint = null) =>
        new(Name: name, Type: type, Endpoint: endpoint is null ? null : new Uri(endpoint), Id: id);

    [Fact]
    public void AssertNoDuplicates_Passes_For_Unique_List()
    {
        var list = new[]
        {
            Make(1, "alpha", PortalType.Manual),
            Make(2, "beta", PortalType.Rss, "https://example.com/feed"),
            Make(3, "gamma", PortalType.Api, "https://api.example.com/jobs"),
        };
        ProviderListValidator.AssertNoDuplicates(list);
    }

    [Fact]
    public void AssertNoDuplicates_Throws_On_Same_Name_Different_Case()
    {
        var list = new[]
        {
            Make(1, "alpha", PortalType.Manual),
            Make(2, "  ALPHA ", PortalType.Manual),
        };
        var ex = Assert.Throws<ConfigException>(() => ProviderListValidator.AssertNoDuplicates(list));
        Assert.Contains("duplicate provider name", ex.Message);
    }

    [Fact]
    public void AssertNoDuplicates_Throws_On_Same_Endpoint_Same_Type()
    {
        var list = new[]
        {
            Make(1, "alpha", PortalType.Rss, "https://example.com/feed"),
            Make(2, "beta",  PortalType.Rss, "https://EXAMPLE.com/feed#anchor"),
        };
        var ex = Assert.Throws<ConfigException>(() => ProviderListValidator.AssertNoDuplicates(list));
        Assert.Contains("duplicate provider endpoint", ex.Message);
    }

    [Fact]
    public void AssertNoDuplicates_Allows_Same_Endpoint_Different_Type()
    {
        var list = new[]
        {
            Make(1, "alpha", PortalType.Rss, "https://example.com/feed"),
            Make(2, "beta",  PortalType.Api, "https://example.com/feed"),
        };
        ProviderListValidator.AssertNoDuplicates(list);
    }

    [Fact]
    public void AssertNoDuplicates_Allows_Multiple_Manual_Without_Endpoint()
    {
        var list = new[]
        {
            Make(1, "alpha", PortalType.Manual),
            Make(2, "beta",  PortalType.Manual),
        };
        ProviderListValidator.AssertNoDuplicates(list);
    }

    [Fact]
    public void NormalizeEndpoint_Lowercases_Scheme_And_Host_And_Strips_Fragment()
    {
        var n = ProviderListValidator.NormalizeEndpoint(new Uri("HTTPS://Example.COM:443/Path?Q=1#frag"));
        Assert.Equal("https://example.com/Path?Q=1", n);
    }

    [Fact]
    public void NormalizeName_Trims_And_Lowercases()
    {
        Assert.Equal("alpha", ProviderListValidator.NormalizeName("  ALPHA "));
    }
}
