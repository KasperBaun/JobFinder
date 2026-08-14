using Jobmatch.Features.Providers;
using Jobmatch.Platform.IO;
using Jobmatch.Platform.Paths;
using Jobmatch;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Features.Providers;

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

    private (ProvidersService svc, UserContext ctx) NewService(ISourceDiscoveryService? discovery = null)
    {
        var ctx = UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);
        var svc = new ProvidersService(
            ctx,
            TestServices.Catalog(ctx),
            TestServices.Runs(ctx),
            new PhysicalFileSystem(),
            new SourceDetectionService(),
            discovery ?? new FakeSourceDiscovery(),
            NullLogger<ProvidersService>.Instance);
        return (svc, ctx);
    }

    [Fact]
    public async Task Create_Greenhouse_PersistsEnabledRemovableProvider()
    {
        var (svc, _) = NewService();

        var created = await svc.CreateAsync("https://boards.greenhouse.io/monzo", "greenhouse", null, CancellationToken.None);

        Assert.True(created.Portal.Id >= UserProviderStore.IdBase);
        var listed = svc.List().Single(p => p.Portal.Id == created.Portal.Id);
        Assert.True(listed.Enabled);
        Assert.Equal("greenhouse-monzo", listed.Portal.Name);
    }

    [Fact]
    public async Task Create_ThenDelete_RemovesFromList()
    {
        var (svc, ctx) = NewService();
        var created = await svc.CreateAsync("https://jobs.lever.co/acmewidgets", "lever", "Acme Widgets", CancellationToken.None);

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
    public async Task Create_WithATypedName_StampsItOnTheListingsToo()
    {
        var (svc, _) = NewService();

        // A tenant not already in the shipped catalog — the store refuses an exact endpoint dupe.
        var created = await svc.CreateAsync(
            "https://abcd.fa.ocs.oraclecloud.com/hcmUI/CandidateExperience/en/sites/CX_1/jobs",
            "oracle",
            "Acme Bank",
            CancellationToken.None);

        Assert.Equal("Acme Bank", created.Portal.DisplayName);
        Assert.Equal("Acme Bank", created.Portal.StaticFields!["company"]);
    }

    [Fact]
    public async Task Create_ManualKind_PersistsManualProvider()
    {
        var (svc, _) = NewService();
        var created = await svc.CreateAsync(null, "manual", "My saved roles", CancellationToken.None);
        Assert.Equal(PortalType.Manual, created.Portal.Type);
        Assert.True(created.Portal.Id >= UserProviderStore.IdBase);
    }

    [Fact]
    public async Task Detect_NewBoard_ReturnsACandidate()
    {
        var (svc, _) = NewService();
        var candidates = await svc.DetectAsync("https://jobs.ashbyhq.com/monzo", CancellationToken.None);
        var c = Assert.Single(candidates);
        Assert.Equal("ashby", c.Kind);
    }

    [Fact]
    public async Task Detect_BoardAlreadyInCatalog_StillResolves()
    {
        var (svc, _) = NewService();
        // 'pleo' is already in the shipped catalog on this endpoint. Detection must not short-circuit
        // on that: the user gets told they have it once the candidate has been fetched and compared,
        // not by a guess made from the URL before anything was fetched.
        var c = Assert.Single(await svc.DetectAsync("https://jobs.ashbyhq.com/pleo", CancellationToken.None));
        Assert.Equal("ashby", c.Kind);
    }

    [Fact]
    public async Task Detect_InvalidUrl_Throws()
    {
        var (svc, _) = NewService();
        await Assert.ThrowsAsync<InvalidRequestException>(() => svc.DetectAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task Detect_UnrecognisedUrl_FallsBackToLinkDiscovery()
    {
        var discovered = new SourceDetectionService().Detect(new Uri("https://boards.greenhouse.io/monzo"))[0];
        var (svc, _) = NewService(new FakeSourceDiscovery(discovered));

        var c = Assert.Single(await svc.DetectAsync("https://example.com/careers", CancellationToken.None));

        Assert.Equal("greenhouse", c.Kind);
    }

    [Fact]
    public async Task Create_FromDiscoveredCandidate_PersistsTheDiscoveredBoard()
    {
        var discovered = new SourceDetectionService().Detect(new Uri("https://jobs.lever.co/acmewidgets"))[0];
        var (svc, _) = NewService(new FakeSourceDiscovery(discovered));

        // The pasted URL is the careers page, not the board — creation has to re-resolve it the same
        // way detection did, or the user would be told "could not recognise a 'lever' source".
        var created = await svc.CreateAsync("https://example.com/careers", "lever", null, CancellationToken.None);

        Assert.Equal("https://api.lever.co/v0/postings/acmewidgets", created.Portal.Endpoint!.ToString());
    }
}
