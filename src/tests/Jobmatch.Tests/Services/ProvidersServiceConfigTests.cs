using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.IO;
using Jobmatch.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Services;

public sealed class ProvidersServiceConfigTests : IDisposable
{
    // Catalog ids used below: 1 = pleo (Ashby, no pagination); 14 = jobindex-rss-softwareudvikler (paginated).
    private const int PaginatedId = 14;
    private const int SingleFetchId = 1;

    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public ProvidersServiceConfigTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ps-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private ProvidersService NewService()
    {
        var ctx = UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);
        return new ProvidersService(ctx, new PhysicalFileSystem(), new SourceDetectionService(), NullLogger<ProvidersService>.Instance);
    }

    [Fact]
    public void SetConfigOverride_Persists_AndGetByIdReflectsIt()
    {
        var svc = NewService();
        svc.SetConfigOverride(PaginatedId, new ProviderOverride(MaxPages: 20, RateLimitRps: 2.0, EnrichBody: true));

        var ov = svc.GetById(PaginatedId).Override;
        Assert.NotNull(ov);
        Assert.Equal(20, ov!.MaxPages);
        Assert.Equal(2.0, ov.RateLimitRps);
        Assert.True(ov.EnrichBody);
    }

    [Fact]
    public void SetConfigOverride_Empty_ResetsToDefault()
    {
        var svc = NewService();
        svc.SetConfigOverride(PaginatedId, new ProviderOverride(MaxPages: 20));
        Assert.NotNull(svc.GetById(PaginatedId).Override);

        svc.SetConfigOverride(PaginatedId, new ProviderOverride());   // all-null => reset

        Assert.Null(svc.GetById(PaginatedId).Override);
    }

    [Fact]
    public void SetConfigOverride_DropsPaginationKnobs_ForSingleFetchSource()
    {
        var svc = NewService();
        svc.SetConfigOverride(SingleFetchId, new ProviderOverride(MaxPages: 10, PageSize: 99, RateLimitRps: 3.0));

        var ov = svc.GetById(SingleFetchId).Override;
        Assert.NotNull(ov);
        Assert.Null(ov!.MaxPages);      // dropped — pleo doesn't paginate
        Assert.Null(ov.PageSize);
        Assert.Equal(3.0, ov.RateLimitRps);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(999, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, null, 0.0)]
    [InlineData(null, null, 99.0)]
    public void SetConfigOverride_RejectsOutOfRangeValues(int? maxPages, int? pageSize, double? rps)
    {
        var svc = NewService();
        Assert.Throws<InvalidRequestException>(() =>
            svc.SetConfigOverride(PaginatedId, new ProviderOverride(MaxPages: maxPages, PageSize: pageSize, RateLimitRps: rps)));
    }

    [Fact]
    public void SetConfigOverride_UnknownId_ThrowsNotFound()
    {
        var svc = NewService();
        Assert.Throws<NotFoundException>(() => svc.SetConfigOverride(999999, new ProviderOverride(RateLimitRps: 2.0)));
    }
}
