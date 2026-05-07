using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderStateTests
{
    [Fact]
    public void LoadOrEmpty_ReturnsEmptyWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var state = ProviderStateLoader.LoadOrEmpty(path);
        Assert.Empty(state.Disabled);
        Assert.Empty(state.Secrets);
    }

    [Fact]
    public void RoundTrip_DisabledIdsAndSecrets()
    {
        var dir = Directory.CreateTempSubdirectory("provider-state-test");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        var input = new ProviderState(
            Disabled: new[] { 3, 7 },
            Secrets: new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc123" },
            });

        ProviderStateLoader.Save(path, input);
        var loaded = ProviderStateLoader.LoadOrEmpty(path);

        Assert.Equal(new[] { 3, 7 }, loaded.Disabled);
        Assert.Equal("abc123", loaded.Secrets[5]["api_key"]);
    }

    [Fact]
    public void Save_AtomicWrite()
    {
        // Saving over an existing file should not leave a .tmp artifact behind.
        var dir = Directory.CreateTempSubdirectory("provider-state-atomic");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        ProviderStateLoader.Save(path, ProviderState.Empty);
        ProviderStateLoader.Save(path, ProviderState.Empty);
        Assert.False(File.Exists(path + ".tmp"));
    }
}
