using Microsoft.AspNetCore.Http.HttpResults;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Configuration;

public sealed class ProvidersHandlerSecretsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private const int JoobleId = 18;          // requiresSecret == "api_key"
    private const int NonSecretId = 1;        // greenhouse-pleo, requiresSecret == null
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

    private JobmatchUserContext NewCtx() =>
        JobmatchUserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);

    [Fact]
    public void SetSecrets_WritesValuesToProviderStateJson()
    {
        var ctx = NewCtx();
        var req = new SetSecretsRequest(new Dictionary<string, string> { [ApiKeyName] = "abc" });

        var result = ProvidersHandler.SetSecrets(JoobleId, req, ctx);

        Assert.IsType<Ok<SaveResponse>>(result);
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.Equal("abc", state.Secrets[JoobleId][ApiKeyName]);
    }

    [Fact]
    public void SetSecrets_EmptyStringClearsValue()
    {
        var ctx = NewCtx();
        ProvidersHandler.SetSecrets(JoobleId,
            new SetSecretsRequest(new Dictionary<string, string> { [ApiKeyName] = "abc" }), ctx);
        ProvidersHandler.SetSecrets(JoobleId,
            new SetSecretsRequest(new Dictionary<string, string> { [ApiKeyName] = "" }), ctx);

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.False(state.Secrets.ContainsKey(JoobleId));
    }

    [Fact]
    public void SetSecrets_UnknownProviderId_Returns404()
    {
        var ctx = NewCtx();
        var result = ProvidersHandler.SetSecrets(99999,
            new SetSecretsRequest(new Dictionary<string, string>()), ctx);
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void SetSecrets_ProviderWithoutRequiresSecret_Returns400()
    {
        var ctx = NewCtx();
        var result = ProvidersHandler.SetSecrets(NonSecretId,
            new SetSecretsRequest(new Dictionary<string, string> { ["whatever"] = "x" }), ctx);
        Assert.IsType<BadRequest<SaveResponse>>(result);
    }
}
