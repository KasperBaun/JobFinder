using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.IO;
using Jobmatch.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Services;

public sealed class ProvidersServiceSourcesTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public ProvidersServiceSourcesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ps-sources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private (ProvidersService svc, UserContext ctx) NewService()
    {
        var ctx = UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);
        var svc = new ProvidersService(ctx, new PhysicalFileSystem(), new SourceDetectionService(), NullLogger<ProvidersService>.Instance);
        return (svc, ctx);
    }

    [Fact]
    public void Create_Greenhouse_PersistsEnabledRemovableProvider()
    {
        var (svc, _) = NewService();

        var created = svc.Create("https://boards.greenhouse.io/monzo", "greenhouse", displayName: null);

        Assert.True(created.Portal.Id >= UserProviderStore.IdBase);
        var listed = svc.List().Single(p => p.Portal.Id == created.Portal.Id);
        Assert.True(listed.Enabled);
        Assert.Equal("greenhouse-monzo", listed.Portal.Name);
    }

    [Fact]
    public void Create_ThenDelete_RemovesFromList()
    {
        var (svc, ctx) = NewService();
        var created = svc.Create("https://jobs.lever.co/acmewidgets", "lever", displayName: "Acme Widgets");

        svc.Delete(created.Portal.Id);

        Assert.DoesNotContain(svc.List(), p => p.Portal.Id == created.Portal.Id);
        Assert.Empty(UserProviderStore.Load(ctx.UserProvidersPath));
    }

    [Fact]
    public void Delete_CatalogProvider_Throws()
    {
        var (svc, _) = NewService();
        Assert.Throws<InvalidRequestException>(() => svc.Delete(1));
    }

    [Fact]
    public void Delete_UnknownUserProvider_ThrowsNotFound()
    {
        var (svc, _) = NewService();
        Assert.Throws<NotFoundException>(() => svc.Delete(UserProviderStore.IdBase + 42));
    }

    [Fact]
    public void Create_ManualKind_PersistsManualProvider()
    {
        var (svc, _) = NewService();
        var created = svc.Create(url: null, "manual", displayName: "My saved roles");
        Assert.Equal(Jobmatch.Models.PortalType.Manual, created.Portal.Type);
        Assert.True(created.Portal.Id >= UserProviderStore.IdBase);
    }

    [Fact]
    public void Detect_NewBoard_ReturnsCandidateWithoutWarning()
    {
        var (svc, _) = NewService();
        // A different Ashby customer than the catalog's 'pleo' — same host, different board → no dup.
        var candidates = svc.Detect("https://jobs.ashbyhq.com/monzo");
        var c = Assert.Single(candidates);
        Assert.Equal("ashby", c.Kind);
        Assert.Null(c.DuplicateWarning);
    }

    [Fact]
    public void Detect_BoardAlreadyInCatalog_WarnsAboutOverlap()
    {
        var (svc, _) = NewService();
        // 'pleo' is in the shipped catalog on the same Ashby endpoint.
        var c = Assert.Single(svc.Detect("https://jobs.ashbyhq.com/pleo"));
        Assert.NotNull(c.DuplicateWarning);
        Assert.Contains("Pleo", c.DuplicateWarning);
    }

    [Fact]
    public void Detect_InvalidUrl_Throws()
    {
        var (svc, _) = NewService();
        Assert.Throws<InvalidRequestException>(() => svc.Detect("   "));
    }
}
