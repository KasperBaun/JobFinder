using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

public sealed class LogLocationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _bootstrapPath;

    public LogLocationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "loglocation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _bootstrapPath = Path.Combine(_tempRoot, "bootstrap.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ResolveRootDir_UsesBootstrapDataDir_WhenConfigured()
    {
        var store = new BootstrapStore(_bootstrapPath);
        store.Save(new BootstrapConfig("me@example.com", @"D:\chosen\data\me", DateTimeOffset.UnixEpoch));

        var fallbackCalled = false;
        var result = LogLocation.ResolveRootDir(store, () => { fallbackCalled = true; return "FALLBACK"; });

        Assert.Equal(@"D:\chosen\data\me", result);
        Assert.False(fallbackCalled);
    }

    [Fact]
    public void ResolveRootDir_UsesFallback_WhenNotConfigured()
    {
        var store = new BootstrapStore(_bootstrapPath); // no file written yet

        var result = LogLocation.ResolveRootDir(store, () => "FALLBACK");

        Assert.Equal("FALLBACK", result);
    }
}
