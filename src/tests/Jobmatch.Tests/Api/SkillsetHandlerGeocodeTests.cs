using Jobmatch.Api.Handlers;
using Jobmatch.Api.Models;
using Jobmatch.Models;
using Jobmatch.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Api;

/// <summary>The re-geocode policy in SkillsetHandler.Update (R-105): geocode new or
/// never-resolved addresses, reuse stored coordinates for unchanged ones, always save.</summary>
public sealed class SkillsetHandlerGeocodeTests
{
    private const string HomeAddress = "Somewhere 1, 2300 København S";

    private static SkillsetUpdateRequest Request(string? address, double? radiusKm = 50) => new(
        Name: "Jane", Location: "Copenhagen", ExperienceYears: 5, TargetRoles: ["Engineer"],
        RemotePreference: "any", Seniority: "mid", PrimaryStack: ["C#"], SecondaryStack: [],
        Domains: [], Disqualifiers: [], Languages: [], EmploymentTypes: [],
        Country: null, Region: null, Metro: [], PreferredCompanies: [],
        Address: address, RadiusKm: radiusKm);

    private static Skillset Existing(string? address, double? lat, double? lon) => new(
        Name: "Jane", Location: "Copenhagen", ExperienceYears: 5, TargetRoles: ["Engineer"],
        RemotePreference: RemotePreference.Any, Seniority: Seniority.Mid,
        PrimaryStack: ["C#"], SecondaryStack: [], Domains: [], Disqualifiers: [],
        Languages: [], EmploymentTypes: [])
    {
        Address = address,
        RadiusKm = 50,
        Latitude = lat,
        Longitude = lon,
        ResolvedAddress = address is null ? null : "Resolved " + address,
    };

    private static (SkillsetHandler Handler, FakeSkillsetService Skillsets, FakeGeocoder Geocoder) Create(
        Skillset? existing, GeocodeResult? geocode)
    {
        var skillsets = new FakeSkillsetService(existing);
        var geocoder = new FakeGeocoder(geocode);
        return (new SkillsetHandler(skillsets, geocoder, NullLogger<SkillsetHandler>.Instance), skillsets, geocoder);
    }

    [Fact]
    public async Task Changed_Address_Geocodes_And_Stores_The_Result()
    {
        var (handler, skillsets, geocoder) = Create(
            Existing("Old Street 1", 55.0, 12.0),
            new GeocodeResult(55.6761, 12.5683, "Resolved " + HomeAddress));

        await handler.Update(Request(HomeAddress));

        Assert.Equal(1, geocoder.Calls);
        Assert.Equal(55.6761, skillsets.LastUpdate!.Latitude);
        Assert.Equal(12.5683, skillsets.LastUpdate.Longitude);
        Assert.Equal("Resolved " + HomeAddress, skillsets.LastUpdate.ResolvedAddress);
    }

    [Fact]
    public async Task Unchanged_Address_With_Stored_Coordinates_Skips_The_Network()
    {
        var (handler, skillsets, geocoder) = Create(Existing(HomeAddress, 55.6761, 12.5683), null);

        await handler.Update(Request(HomeAddress));

        Assert.Equal(0, geocoder.Calls);
        Assert.Equal(55.6761, skillsets.LastUpdate!.Latitude);
        Assert.Equal(12.5683, skillsets.LastUpdate.Longitude);
    }

    [Fact]
    public async Task Unchanged_Address_Without_Coordinates_Geocodes_Again()
    {
        var (handler, _, geocoder) = Create(
            Existing(HomeAddress, null, null),
            new GeocodeResult(55.6761, 12.5683, HomeAddress));

        await handler.Update(Request(HomeAddress));

        Assert.Equal(1, geocoder.Calls);
    }

    [Fact]
    public async Task Failed_Geocode_Still_Saves_Without_Coordinates()
    {
        var (handler, skillsets, geocoder) = Create(null, geocode: null);

        await handler.Update(Request(HomeAddress));

        Assert.Equal(1, geocoder.Calls);
        Assert.NotNull(skillsets.LastUpdate);
        Assert.Equal(HomeAddress, skillsets.LastUpdate!.Address);
        Assert.Null(skillsets.LastUpdate.Latitude);
        Assert.Null(skillsets.LastUpdate.Longitude);
    }

    [Fact]
    public async Task Blank_Address_Clears_Without_A_Network_Call()
    {
        var (handler, skillsets, geocoder) = Create(Existing(HomeAddress, 55.6761, 12.5683), null);

        await handler.Update(Request("  "));

        Assert.Equal(0, geocoder.Calls);
        Assert.Null(skillsets.LastUpdate!.Latitude);
        Assert.Null(skillsets.LastUpdate.Longitude);
        Assert.Null(skillsets.LastUpdate.ResolvedAddress);
    }

    private sealed class FakeSkillsetService(Skillset? existing) : ISkillsetService
    {
        public SkillsetUpdate? LastUpdate { get; private set; }

        public Skillset Get() => existing ?? throw new InvalidRequestException("No profile set up yet.");

        public Skillset? Find() => existing;

        public Skillset Update(SkillsetUpdate input)
        {
            LastUpdate = input;
            return existing ?? Existing(input.Address, input.Latitude, input.Longitude);
        }
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
