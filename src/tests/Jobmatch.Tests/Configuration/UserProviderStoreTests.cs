using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class UserProviderStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public UserProviderStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "user-providers.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static PortalConfig RssDraft(string name = "rss-example") => new(
        Name: name,
        Type: PortalType.Rss,
        Endpoint: new Uri("https://example.com/jobs.rss"),
        DisplayName: "Example (feed)");

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        Assert.Empty(UserProviderStore.Load(_path));
    }

    [Fact]
    public void Add_AssignsIdAtBase_AndRoundTrips()
    {
        var created = UserProviderStore.Add(_path, RssDraft(), catalog: []);
        Assert.Equal(UserProviderStore.IdBase, created.Id);

        var loaded = UserProviderStore.Load(_path);
        Assert.Single(loaded);
        Assert.Equal(UserProviderStore.IdBase, loaded[0].Id);
        Assert.Equal(PortalType.Rss, loaded[0].Type);
        Assert.Equal("https://example.com/jobs.rss", loaded[0].Endpoint!.ToString());
    }

    [Fact]
    public void Add_ApiDraftWithMapping_RoundTripsMappingAndQuery()
    {
        var draft = new PortalConfig(
            Name: "greenhouse-monzo",
            Type: PortalType.Api,
            Endpoint: new Uri("https://boards-api.greenhouse.io/v1/boards/monzo/jobs"),
            QueryParams: new Dictionary<string, object?> { ["content"] = "true" },
            ResponseMapping: new Dictionary<string, string> { ["items_path"] = "jobs", ["url"] = "absolute_url" },
            StaticFields: new Dictionary<string, string> { ["company"] = "Monzo" },
            DisplayName: "Monzo");

        UserProviderStore.Add(_path, draft, catalog: []);
        var loaded = UserProviderStore.Load(_path)[0];

        Assert.Equal("jobs", loaded.ResponseMapping!["items_path"]);
        Assert.Equal("absolute_url", loaded.ResponseMapping!["url"]);
        Assert.Equal("true", loaded.QueryParams!["content"]);
        Assert.Equal("Monzo", loaded.StaticFields!["company"]);
    }

    [Fact]
    public void Add_SecondProvider_GetsNextId()
    {
        UserProviderStore.Add(_path, RssDraft("rss-a"), catalog: []);
        var second = UserProviderStore.Add(_path, RssDraft("rss-b") with { Endpoint = new Uri("https://b.example/jobs.rss") }, catalog: []);
        Assert.Equal(UserProviderStore.IdBase + 1, second.Id);
        Assert.Equal(2, UserProviderStore.Load(_path).Count);
    }

    [Fact]
    public void Add_DuplicateNameAgainstCatalog_Throws()
    {
        var catalog = new List<PortalConfig> { RssDraft("rss-example") with { Id = 5 } };
        Assert.Throws<ConfigException>(() => UserProviderStore.Add(_path, RssDraft("rss-example"), catalog));
    }

    [Fact]
    public void Remove_ExistingId_RemovesIt()
    {
        var created = UserProviderStore.Add(_path, RssDraft(), catalog: []);
        Assert.True(UserProviderStore.Remove(_path, created.Id));
        Assert.Empty(UserProviderStore.Load(_path));
    }

    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        UserProviderStore.Add(_path, RssDraft(), catalog: []);
        Assert.False(UserProviderStore.Remove(_path, 999999));
    }
}
