using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.IO;
using Jobmatch.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Configuration;

public sealed class ProvidersHandlerSecretsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private const int JoobleId = 18;          // requiresSecret == "api_key"
    private const int NonSecretId = 1;        // pleo (Ashby), requiresSecret == null
    private const string ApiKeyName = "api_key";

    public ProvidersHandlerSecretsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ph-secrets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private UserContext NewCtx() =>
        UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);

    private static ProvidersService NewService(UserContext ctx) =>
        new(ctx, new PhysicalFileSystem(), NullLogger<ProvidersService>.Instance);

    [Fact]
    public void SetSecrets_WritesValuesToProviderStateJson()
    {
        var ctx = NewCtx();
        var svc = NewService(ctx);

        svc.SetSecrets(JoobleId, new Dictionary<string, string> { [ApiKeyName] = "abc" });

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.Equal("abc", state.Secrets[JoobleId][ApiKeyName]);
    }

    [Fact]
    public void SetSecrets_EmptyStringClearsValue()
    {
        var ctx = NewCtx();
        var svc = NewService(ctx);

        svc.SetSecrets(JoobleId, new Dictionary<string, string> { [ApiKeyName] = "abc" });
        svc.SetSecrets(JoobleId, new Dictionary<string, string> { [ApiKeyName] = "" });

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.False(state.Secrets.ContainsKey(JoobleId));
    }

    [Fact]
    public void SetSecrets_UnknownProviderId_Throws()
    {
        var ctx = NewCtx();
        var svc = NewService(ctx);

        Assert.Throws<NotFoundException>(() =>
            svc.SetSecrets(99999, new Dictionary<string, string>()));
    }

    [Fact]
    public void SetSecrets_ProviderWithoutRequiresSecret_Throws()
    {
        var ctx = NewCtx();
        var svc = NewService(ctx);

        Assert.Throws<InvalidRequestException>(() =>
            svc.SetSecrets(NonSecretId, new Dictionary<string, string> { ["whatever"] = "x" }));
    }
}
