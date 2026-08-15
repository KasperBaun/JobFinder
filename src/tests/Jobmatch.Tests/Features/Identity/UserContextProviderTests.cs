using Jobmatch.Features.Bootstrap;
using Jobmatch;

namespace Jobmatch.Tests.Features.Identity;

public sealed class UserContextProviderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _bootstrapPath;
    private readonly string? _envBackup;

    public UserContextProviderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ucp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _bootstrapPath = Path.Combine(_tempRoot, "bootstrap.json");
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private UserContextProvider NewProvider() => new(new BootstrapStore(_bootstrapPath));

    [Fact]
    public void FreshProvider_IsNotConfigured_And_CurrentThrows()
    {
        var provider = NewProvider();

        Assert.False(provider.IsConfigured);
        Assert.Throws<SetupRequiredException>(() => provider.Current);

        var state = provider.State();
        Assert.False(state.IsConfigured);
        Assert.False(string.IsNullOrWhiteSpace(state.SuggestedDataDir));
        Assert.Equal(_bootstrapPath, state.BootstrapPath);
    }

    [Fact]
    public void Complete_CreatesDirectory_PersistsChoice_AndConfigures()
    {
        var dataDir = Path.Combine(_tempRoot, "chosen");
        var provider = NewProvider();

        var ctx = provider.Complete("me@example.com", dataDir);

        Assert.True(provider.IsConfigured);
        Assert.Equal(Path.GetFullPath(dataDir), ctx.RootDir);
        Assert.True(Directory.Exists(dataDir));
        Assert.True(File.Exists(_bootstrapPath));

        // A brand-new provider reading the same bootstrap file starts already configured.
        var reloaded = NewProvider();
        Assert.True(reloaded.IsConfigured);
        Assert.Equal(Path.GetFullPath(dataDir), reloaded.Current.RootDir);
        Assert.Equal("me@example.com", reloaded.Current.Email);
    }

    [Fact]
    public void Complete_DoesNotSeedProfile_AndProfileExistsReflectsFile()
    {
        var dataDir = Path.Combine(_tempRoot, "chosen");
        var provider = NewProvider();

        var ctx = provider.Complete("me@example.com", dataDir);

        // No generic profile is seeded on first-run setup anymore.
        Assert.False(File.Exists(ctx.SkillsetPath));
        Assert.False(provider.State().ProfileExists);

        // Once a profile file exists, State reflects it.
        File.WriteAllText(ctx.SkillsetPath, "placeholder");
        Assert.True(provider.State().ProfileExists);
    }

    [Fact]
    public void Complete_RequiresEmailAndDataDir()
    {
        var provider = NewProvider();

        Assert.Throws<InvalidRequestException>(() => provider.Complete("", Path.Combine(_tempRoot, "d")));
        Assert.Throws<InvalidRequestException>(() => provider.Complete("me@example.com", "  "));
    }

    [Fact]
    public void Complete_PersistsNormalisedLanguage()
    {
        var provider = NewProvider();
        provider.Complete("me@example.com", Path.Combine(_tempRoot, "chosen"), " DA ");

        Assert.Equal("da", provider.State().Language);
        Assert.Equal("da", NewProvider().State().Language);
    }

    [Fact]
    public void Complete_WithUnsupportedOrMissingLanguage_LeavesItUnset()
    {
        var provider = NewProvider();
        provider.Complete("me@example.com", Path.Combine(_tempRoot, "chosen"), "klingon");

        // Setup must not fail over a language the GUI doesn't ship — it just falls back to English.
        Assert.Null(provider.State().Language);
    }

    [Fact]
    public void BootstrapWrittenBeforeLanguageExisted_StillLoads()
    {
        var dataDir = Path.Combine(_tempRoot, "chosen");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(
            _bootstrapPath,
            $$"""{"email":"me@example.com","dataDir":{{System.Text.Json.JsonSerializer.Serialize(dataDir)}},"acknowledgedAt":"2026-01-01T00:00:00+00:00"}""");

        var provider = NewProvider();

        Assert.True(provider.IsConfigured);
        Assert.Null(provider.State().Language);
    }

    [Fact]
    public void SetLanguage_PersistsAndPreservesTheRestOfTheRecord()
    {
        var dataDir = Path.Combine(_tempRoot, "chosen");
        var provider = NewProvider();
        provider.Complete("me@example.com", dataDir);

        Assert.Equal("da", provider.SetLanguage("da"));

        var reloaded = NewProvider();
        Assert.Equal("da", reloaded.State().Language);
        Assert.Equal("me@example.com", reloaded.Current.Email);
        Assert.Equal(Path.GetFullPath(dataDir), reloaded.Current.RootDir);
    }

    [Fact]
    public void SetLanguage_RejectsUnsupportedValues()
    {
        var provider = NewProvider();
        provider.Complete("me@example.com", Path.Combine(_tempRoot, "chosen"));

        Assert.Throws<InvalidRequestException>(() => provider.SetLanguage("klingon"));
        Assert.Throws<InvalidRequestException>(() => provider.SetLanguage(null));
    }

    [Fact]
    public void SetLanguage_BeforeSetup_RequiresSetup()
    {
        Assert.Throws<SetupRequiredException>(() => NewProvider().SetLanguage("da"));
    }
}
