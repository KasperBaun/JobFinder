using Jobmatch;
using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

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
}
