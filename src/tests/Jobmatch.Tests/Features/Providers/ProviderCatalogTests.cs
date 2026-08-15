using Jobmatch.Features.Providers;
using Jobmatch.Infrastructure.Json;
using Jobmatch.Infrastructure.Paths;
using System.Text.Json;

namespace Jobmatch.Tests.Features.Providers;

/// <summary>
/// The catalog is the single answer to "what sources does this user have?". These pin the property
/// that used to be broken: a source the user added themselves is part of that answer for every
/// caller, not just the providers page.
/// </summary>
public sealed class ProviderCatalogTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly UserContext _ctx;

    public ProviderCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "provider-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _ctx = UserContext.Resolve(emailOverride: "catalog@example.com", repoRoot: _tempRoot, seedExamples: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private void AddUserProvider(string name, string endpoint = "https://boards.example/feed.rss")
    {
        var draft = new PortalConfig(
            Name: name,
            Type: PortalType.Rss,
            Enabled: true,
            Endpoint: new Uri(endpoint));
        UserProviderStore.Add(_ctx.UserProvidersPath, draft, catalog: []);
    }

    [Fact]
    public void All_IncludesUserAddedSourcesAlongsideTheShippedCatalog()
    {
        var shippedCount = new ProviderCatalog(_ctx).Shipped().Count;
        AddUserProvider("my-own-board");

        var all = new ProviderCatalog(_ctx).All();

        Assert.Equal(shippedCount + 1, all.Count);
        Assert.Contains(all, p => p.Name == "my-own-board");
    }

    [Fact]
    public void Shipped_ExcludesUserAddedSources()
    {
        AddUserProvider("my-own-board");

        Assert.DoesNotContain(new ProviderCatalog(_ctx).Shipped(), p => p.Name == "my-own-board");
    }

    [Fact]
    public void Effective_KeepsAUserAddedSourceEnabled_SoASearchRunFetchesIt()
    {
        AddUserProvider("my-own-board");

        var enabled = new ProviderCatalog(_ctx).Effective().Where(p => p.Enabled).ToList();

        Assert.Contains(enabled, p => p.Name == "my-own-board");
    }

    [Fact]
    public void Effective_HonoursAnOptOutOfAUserAddedSource()
    {
        AddUserProvider("my-own-board");
        var catalog = new ProviderCatalog(_ctx);
        var id = catalog.All().Single(p => p.Name == "my-own-board").Id;
        WriteState("{\"disabled\":[" + id + "]}");

        var effective = catalog.Effective().Single(p => p.Name == "my-own-board");

        Assert.False(effective.Enabled);
    }

    [Fact]
    public void Effective_AppliesAPerSourceOverride()
    {
        AddUserProvider("my-own-board");
        var catalog = new ProviderCatalog(_ctx);
        var id = catalog.All().Single(p => p.Name == "my-own-board").Id;
        WriteState("{\"overrides\":{\"" + id + "\":{\"rateLimitRps\":0.5}}}");

        Assert.Equal(0.5, catalog.Effective().Single(p => p.Name == "my-own-board").RateLimitRps);
    }

    private void WriteState(string json) => File.WriteAllText(_ctx.ProviderStatePath, json);

    [Fact]
    public void All_IsEmptyOfUserSources_WhenNoneHaveBeenAdded()
    {
        var all = new ProviderCatalog(_ctx).All();

        Assert.Equal(new ProviderCatalog(_ctx).Shipped().Count, all.Count);
    }

    [Fact]
    public void UserProvidersFile_RoundTripsThroughTheSharedJsonPolicy()
    {
        AddUserProvider("my-own-board");

        using var doc = JsonDocument.Parse(File.ReadAllText(_ctx.UserProvidersPath));

        // camelCase members and camelCase enum values — the policy Platform/Json owns.
        var provider = doc.RootElement.GetProperty("providers")[0];
        Assert.Equal("my-own-board", provider.GetProperty("name").GetString());
        Assert.Equal("rss", provider.GetProperty("type").GetString());
        Assert.Equal(JobmatchJsonOptions.Indented.PropertyNamingPolicy, JsonNamingPolicy.CamelCase);
    }
}
