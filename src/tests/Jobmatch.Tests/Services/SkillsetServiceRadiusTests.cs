using Jobmatch.Features.Skillsets;
using Jobmatch.Platform.Paths;
using Jobmatch;

namespace Jobmatch.Tests.Services;

/// <summary>Merge semantics of the radius-filter fields in SkillsetService (R-105).</summary>
public sealed class SkillsetServiceRadiusTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public SkillsetServiceRadiusTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "skillset-radius-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private SkillsetService New(IGeocodingService? geocoding = null)
    {
        var ctx = UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);
        return new SkillsetService(ctx, geocoding ?? new NullGeocoder());
    }

    private static SkillsetUpdate Essentials(string? address = null, double? radiusKm = null) => new(
        Name: "Jane Doe", Location: "Copenhagen", ExperienceYears: 5,
        TargetRoles: ["Backend Engineer"], RemotePreference: "remote", Seniority: "senior",
        PrimaryStack: ["C#"], SecondaryStack: null, Domains: null, Disqualifiers: null,
        Languages: null, EmploymentTypes: null, Country: null, Region: null, Metro: null,
        Address: address, RadiusKm: radiusKm);

    private sealed class FixedGeocoder(GeocodeResult result) : IGeocodingService
    {
        public Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default) =>
            Task.FromResult<GeocodeResult?>(result);
    }

    [Fact]
    public async Task Update_Persists_Address_Radius_And_Coordinates()
    {
        var svc = New(new FixedGeocoder(
            new GeocodeResult(55.6761, 12.5683, "Somewhere 1, 2300 København S")));
        await svc.UpdateAsync(Essentials(address: "Somewhere 1, 2300 København S", radiusKm: 50));

        var reloaded = svc.Get();
        Assert.Equal("Somewhere 1, 2300 København S", reloaded.Address);
        Assert.Equal(50, reloaded.RadiusKm);
        Assert.Equal(55.6761, reloaded.Latitude);
        Assert.Equal(12.5683, reloaded.Longitude);
    }

    [Fact]
    public async Task Update_With_Blank_Address_Clears_Address_And_Coordinates()
    {
        var svc = New(new FixedGeocoder(new GeocodeResult(55.0, 12.0, "Somewhere 1")));
        await svc.UpdateAsync(Essentials(address: "Somewhere 1", radiusKm: 50));

        await svc.UpdateAsync(Essentials(address: "  "));

        var reloaded = svc.Get();
        Assert.Null(reloaded.Address);
        Assert.Null(reloaded.Latitude);
        Assert.Null(reloaded.Longitude);
        Assert.Null(reloaded.ResolvedAddress);
        Assert.Equal(50, reloaded.RadiusKm); // radius is kept — it's a preference, not derived data
    }

    [Fact]
    public async Task Update_Null_Radius_Keeps_Existing()
    {
        var svc = New();
        await svc.UpdateAsync(Essentials(radiusKm: 25));
        await svc.UpdateAsync(Essentials());

        Assert.Equal(25, svc.Get().RadiusKm);
    }

    [Fact]
    public async Task Update_Negative_Radius_Throws()
    {
        var svc = New();
        var ex = await Assert.ThrowsAsync<ConfigException>(() => svc.UpdateAsync(Essentials(radiusKm: -1)));
        Assert.Contains("radiusKm", ex.Message);
    }

    [Fact]
    public async Task Find_Returns_Null_Before_First_Save_Then_The_Profile()
    {
        var svc = New();
        Assert.Null(svc.Find());

        await svc.UpdateAsync(Essentials());
        Assert.NotNull(svc.Find());
    }
}
