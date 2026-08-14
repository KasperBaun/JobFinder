using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Geo;

namespace Jobmatch.Tests.Geo;

public sealed class RadiusFilterTests
{
    private static readonly Gazetteer TestGazetteer = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København"], 55.6761, 12.5683, "DK", GeoPlaceType.City, 1_150_000),
        new GazetteerEntry("Aarhus", ["Århus"], 56.1567, 10.2108, "DK", GeoPlaceType.City, 285_000),
        new GazetteerEntry("Warsaw", ["Warszawa"], 52.2298, 21.0118, "PL", GeoPlaceType.City, 1_700_000),
        new GazetteerEntry("Aalborg", ["Ålborg"], 57.0480, 9.9187, "DK", GeoPlaceType.City, 142_000),
        new GazetteerEntry("Glostrup", [], 55.6666, 12.4038, "DK", GeoPlaceType.City, 23_000),
        new GazetteerEntry("Region Nordjylland", ["Nordjylland"], 57.3072, 10.1128, "DK", GeoPlaceType.Region, 0),
        new GazetteerEntry("Denmark", ["DK", "Danmark"], 55.6761, 12.5683, "DK", GeoPlaceType.Country, 5_800_000),
        new GazetteerEntry("Poland", ["PL", "Polska"], 52.2298, 21.0118, "PL", GeoPlaceType.Country, 37_900_000),
    ]);

    private static Skillset HomeInCopenhagen(double? radiusKm = 50, double? lat = 55.6761, double? lon = 12.5683) => new(
        Name: "Test", Location: "Copenhagen, Denmark", ExperienceYears: 5, TargetRoles: [],
        RemotePreference: RemotePreference.Any, Seniority: Seniority.Any,
        PrimaryStack: [], SecondaryStack: [], Domains: [], Disqualifiers: [],
        Languages: [], EmploymentTypes: [])
    {
        Address = "Somewhere 1, 2300 København S",
        RadiusKm = radiusKm,
        Latitude = lat,
        Longitude = lon,
    };

    private static Listing L(string? location, RemoteMode mode = RemoteMode.Onsite) => new(
        Id: "id-1", Portal: "test", Title: "Engineer", Company: null, Location: location,
        RemoteMode: mode, Description: "", Url: new Uri("https://example.com/1"),
        PostedAt: null, FetchedAt: DateTimeOffset.UtcNow, Raw: default(JsonElement));

    [Fact]
    public void Create_Is_Null_When_Coordinates_Missing()
    {
        Assert.Null(RadiusFilter.Create(HomeInCopenhagen(lat: null), TestGazetteer));
        Assert.Null(RadiusFilter.Create(HomeInCopenhagen(lon: null), TestGazetteer));
    }

    [Fact]
    public void Create_Is_Null_When_Radius_Missing_Or_Zero()
    {
        Assert.Null(RadiusFilter.Create(HomeInCopenhagen(radiusKm: null), TestGazetteer));
        Assert.Null(RadiusFilter.Create(HomeInCopenhagen(radiusKm: 0), TestGazetteer));
    }

    [Fact]
    public void Evaluate_Remote_Listing_Is_Exempt()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        Assert.Null(filter.Evaluate(L("Warsaw", RemoteMode.Remote)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Atlantis")]
    public void Evaluate_Missing_Or_Unresolvable_Location_Passes(string? location)
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        Assert.Null(filter.Evaluate(L(location)));
    }

    [Theory]
    [InlineData(RemoteMode.Onsite)]
    [InlineData(RemoteMode.Hybrid)]
    [InlineData(RemoteMode.Unknown)]
    public void Evaluate_Far_Listing_Drops_For_Every_NonRemote_Mode(RemoteMode mode)
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        var verdict = filter.Evaluate(L("Warsaw, Poland", mode));
        Assert.NotNull(verdict);
        Assert.InRange(verdict!.Km, 660, 680);
        Assert.Equal(50, verdict.MaxKm);
        Assert.Equal("Warsaw", verdict.Place);
    }

    [Fact]
    public void Evaluate_Within_Radius_Passes()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        Assert.Null(filter.Evaluate(L("Copenhagen")));
    }

    [Fact]
    public void Evaluate_MultiLocation_Takes_Minimum_Distance()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        // One site is home — passes even though the other is far away.
        Assert.Null(filter.Evaluate(L("Warsaw / Copenhagen")));

        // Both sites out of range — verdict names the nearest one.
        var verdict = filter.Evaluate(L("Warsaw, Aarhus"));
        Assert.NotNull(verdict);
        Assert.Equal("Aarhus", verdict!.Place);
        Assert.InRange(verdict.Km, 154, 160);
    }

    [Fact]
    public void Evaluate_TrailingCountry_Does_Not_Rescue_A_Far_City()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        var verdict = filter.Evaluate(L("Aarhus, Denmark"));
        Assert.NotNull(verdict);
        Assert.Equal("Aarhus", verdict!.Place);
        Assert.InRange(verdict.Km, 154, 160);
    }

    [Theory]
    [InlineData("Warsaw og Copenhagen")]
    [InlineData("Aalborg & Glostrup")]
    public void Evaluate_Conjunction_Finds_The_Near_Site(string location)
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        Assert.Null(filter.Evaluate(L(location)));
    }

    [Theory]
    [InlineData("Warsaw og Aalborg", "Aalborg")]
    [InlineData("Warsaw & Aarhus", "Aarhus")]
    public void Evaluate_Conjunction_Verdict_Names_The_Nearest_Site(string location, string expected)
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        var verdict = filter.Evaluate(L(location));
        Assert.NotNull(verdict);
        Assert.Equal(expected, verdict!.Place);
    }

    // An area in the user's own country is stored as one centroid but covers ground the user may
    // live on, so measuring it as a point would hide a job next door. It reads as "somewhere in
    // Denmark" — no site — and passes like any location the gazetteer can't place.
    [Theory]
    [InlineData("Nordjylland, Danmark")]
    [InlineData("Nordjylland")]
    [InlineData("Danmark")]
    [InlineData("Ikke angivet, Danmark")]
    public void Evaluate_Area_In_The_Home_Country_Is_Not_A_Site(string location)
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(radiusKm: 5), TestGazetteer)!;
        Assert.Null(filter.Evaluate(L(location)));
    }

    // The city named beside the home country is still a site — only the coarse match is waived.
    [Fact]
    public void Evaluate_HomeCountry_Waiver_Does_Not_Rescue_A_Named_City()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        var verdict = filter.Evaluate(L("Aarhus, Danmark"));
        Assert.NotNull(verdict);
        Assert.Equal("Aarhus", verdict!.Place);
    }

    [Fact]
    public void Evaluate_CountryOnly_Location_Still_Drops()
    {
        var filter = RadiusFilter.Create(HomeInCopenhagen(), TestGazetteer)!;
        var verdict = filter.Evaluate(L("Poland"));
        Assert.NotNull(verdict);
        Assert.Equal("Poland", verdict!.Place);
        Assert.InRange(verdict.Km, 660, 680);
    }
}
