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
        Assert.Empty(state.Enabled);
        Assert.Empty(state.Secrets);
    }

    [Fact]
    public void RoundTrip_DisabledEnabledAndSecrets()
    {
        var dir = Directory.CreateTempSubdirectory("provider-state-test");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        var input = new ProviderState(
            Disabled: new[] { 3, 7 },
            Enabled: new[] { 15, 16 },
            Secrets: new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc123" },
            },
            Overrides: new Dictionary<int, ProviderOverride>
            {
                [14] = new ProviderOverride(MaxPages: 20, RateLimitRps: 2.0, EnrichBody: true),
            });

        ProviderStateLoader.Save(path, input);
        var loaded = ProviderStateLoader.LoadOrEmpty(path);

        Assert.Equal(new[] { 3, 7 }, loaded.Disabled);
        Assert.Equal(new[] { 15, 16 }, loaded.Enabled);
        Assert.Equal("abc123", loaded.Secrets[5]["api_key"]);
        Assert.Equal(20, loaded.Overrides[14].MaxPages);
        Assert.Equal(2.0, loaded.Overrides[14].RateLimitRps);
        Assert.True(loaded.Overrides[14].EnrichBody);
        Assert.Null(loaded.Overrides[14].PageSize);
    }

    [Fact]
    public void LoadOrEmpty_LegacyFileWithoutOverridesField_LoadsAsEmpty()
    {
        var dir = Directory.CreateTempSubdirectory("provider-state-legacy-ov");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        File.WriteAllText(path, """{"disabled":[10],"enabled":[],"secrets":{}}""");

        var loaded = ProviderStateLoader.LoadOrEmpty(path);

        Assert.Empty(loaded.Overrides);
    }

    [Fact]
    public void LoadOrEmpty_LegacyFileWithoutEnabledField_LoadsAsEmptyEnabled()
    {
        // Existing user state files predate the symmetric opt-in/opt-out model;
        // they only carry `disabled`. The loader must treat missing `enabled` as [].
        var dir = Directory.CreateTempSubdirectory("provider-state-legacy");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        File.WriteAllText(path, """{"disabled":[10,11],"secrets":{}}""");

        var loaded = ProviderStateLoader.LoadOrEmpty(path);

        Assert.Equal(new[] { 10, 11 }, loaded.Disabled);
        Assert.Empty(loaded.Enabled);
    }

    [Fact]
    public void Save_AtomicWrite()
    {
        var dir = Directory.CreateTempSubdirectory("provider-state-atomic");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        ProviderStateLoader.Save(path, ProviderState.Empty);
        ProviderStateLoader.Save(path, ProviderState.Empty);
        Assert.False(File.Exists(path + ".tmp"));
    }
}
