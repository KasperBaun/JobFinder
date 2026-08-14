using Jobmatch.Domain;
using Jobmatch.Features.Skillsets;
using JobmatchUserContext = Jobmatch.Platform.Paths.UserContext;

namespace Jobmatch.Tests.Features.Skillsets;

/// <summary>
/// The re-geocode policy (R-105): geocode new or never-resolved addresses, reuse stored coordinates
/// for an unchanged one, and always save — a lookup that fails or times out costs the coordinates,
/// never the profile. The rule lives in the service, so a profile's stored position cannot disagree
/// with its address no matter which caller writes it.
/// </summary>
public sealed class SkillsetServiceGeocodeTests : IDisposable
{
    private const string HomeAddress = "Somewhere 1, 2300 København S";

    private readonly string _tempRoot;
    private readonly JobmatchUserContext _ctx;

    public SkillsetServiceGeocodeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "skillset-geocode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _ctx = JobmatchUserContext.Resolve(
            emailOverride: "geocode@example.com", repoRoot: _tempRoot, seedExamples: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static SkillsetUpdate Update(string? address) => new(
        Name: "Jane", Location: "Copenhagen", ExperienceYears: 5, TargetRoles: ["Engineer"],
        RemotePreference: "any", Seniority: "mid", PrimaryStack: ["C#"], SecondaryStack: [],
        Domains: [], Disqualifiers: [], Languages: [], EmploymentTypes: [],
        Country: null, Region: null, Metro: [], PreferredCompanies: [],
        Address: address, RadiusKm: 50);

    private SkillsetService Service(FakeGeocoder geocoder) => new(_ctx, geocoder);

    [Fact]
    public async Task ANewAddressIsGeocodedAndItsCoordinatesStored()
    {
        var geocoder = new FakeGeocoder(new GeocodeResult(55.6761, 12.5683, "Resolved " + HomeAddress));

        var saved = await Service(geocoder).UpdateAsync(Update(HomeAddress));

        Assert.Equal(1, geocoder.Calls);
        Assert.Equal(55.6761, saved.Latitude);
        Assert.Equal(12.5683, saved.Longitude);
        Assert.Equal("Resolved " + HomeAddress, saved.ResolvedAddress);
    }

    [Fact]
    public async Task AnUnchangedAddressWithStoredCoordinatesSkipsTheNetwork()
    {
        var first = new FakeGeocoder(new GeocodeResult(55.6761, 12.5683, "Resolved " + HomeAddress));
        await Service(first).UpdateAsync(Update(HomeAddress));

        var second = new FakeGeocoder(null);
        var saved = await Service(second).UpdateAsync(Update(HomeAddress));

        Assert.Equal(0, second.Calls);
        Assert.Equal(55.6761, saved.Latitude);
        Assert.Equal(12.5683, saved.Longitude);
    }

    [Fact]
    public async Task AnUnchangedAddressWithoutCoordinatesIsGeocodedAgain()
    {
        var first = new FakeGeocoder(null);
        await Service(first).UpdateAsync(Update(HomeAddress));

        var second = new FakeGeocoder(new GeocodeResult(55.6761, 12.5683, HomeAddress));
        await Service(second).UpdateAsync(Update(HomeAddress));

        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task AFailedLookupStillSavesTheProfile()
    {
        var geocoder = new FakeGeocoder(null);

        var saved = await Service(geocoder).UpdateAsync(Update(HomeAddress));

        Assert.Equal(1, geocoder.Calls);
        Assert.Equal(HomeAddress, saved.Address);
        Assert.Null(saved.Latitude);
        Assert.Null(saved.Longitude);
    }

    [Fact]
    public async Task ABlankAddressClearsTheCoordinatesWithoutALookup()
    {
        var first = new FakeGeocoder(new GeocodeResult(55.6761, 12.5683, "Resolved " + HomeAddress));
        await Service(first).UpdateAsync(Update(HomeAddress));

        var second = new FakeGeocoder(null);
        var saved = await Service(second).UpdateAsync(Update("  "));

        Assert.Equal(0, second.Calls);
        Assert.Null(saved.Latitude);
        Assert.Null(saved.Longitude);
        Assert.Null(saved.ResolvedAddress);
    }

    private sealed class FakeGeocoder(GeocodeResult? result) : IGeocodingService
    {
        public int Calls { get; private set; }

        public Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
