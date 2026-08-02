using Jobmatch.Geo;

namespace Jobmatch.Tests.Geo;

public sealed class GazetteerTests
{
    // The real bundled TSV — also proves the csproj bundling copies it next to the binaries.
    private static readonly Gazetteer Bundled = Gazetteer.LoadBundled();

    [Fact]
    public void Resolve_DanishPostalCode_Beats_Everything()
    {
        var place = Bundled.Resolve("2300 København S", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Postal, place!.Type);
        Assert.Equal("DK", place.CountryCode);
        Assert.InRange(place.Latitude, 55.6, 55.7);
    }

    [Fact]
    public void Resolve_DanishCity_ByName()
    {
        var place = Bundled.Resolve("Odense", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.City, place!.Type);
        Assert.Equal("DK", place.CountryCode);
    }

    [Theory]
    [InlineData("Århus")]
    [InlineData("Aarhus")]
    [InlineData("aarhus")]
    public void Resolve_AsciiFolding_Bridges_Aarhus_Spellings(string spelling)
    {
        var place = Bundled.Resolve(spelling, "DK");
        Assert.NotNull(place);
        Assert.Equal("DK", place!.CountryCode);
        Assert.InRange(place.Latitude, 56.1, 56.2);
    }

    [Theory]
    [InlineData("Warszawa", "PL")]
    [InlineData("Warsaw", "PL")]
    [InlineData("Dhaka", "BD")]
    public void Resolve_WorldCity_Without_Country(string city, string expectedCc)
    {
        var place = Bundled.Resolve(city, "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.City, place!.Type);
        Assert.Equal(expectedCc, place.CountryCode);
    }

    [Fact]
    public void Resolve_CountryOnly_Hits_Capital_Centroid()
    {
        var place = Bundled.Resolve("Poland", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Country, place!.Type);
        Assert.Equal("PL", place.CountryCode);
        Assert.InRange(place.Latitude, 52.0, 52.5); // Warsaw
    }

    [Fact]
    public void Resolve_Specificity_Postal_Beats_Country()
    {
        var place = Bundled.Resolve("8000 Aarhus C, Denmark", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Postal, place!.Type);
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData("Remote — Warsaw")]
    [InlineData("Worldwide")]
    [InlineData("Anywhere (global)")]
    public void Resolve_RemoteTokens_Skip_Resolution(string location)
    {
        Assert.Null(Bundled.Resolve(location, "DK"));
        Assert.Empty(Bundled.ResolveAll(location, "DK"));
    }

    [Fact]
    public void Resolve_WholeSegment_Only_No_Substring_Hits()
    {
        Assert.Null(Bundled.Resolve("Development office near Copenhagen", "DK"));
    }

    [Fact]
    public void Resolve_Null_Or_Blank_Is_Null()
    {
        Assert.Null(Bundled.Resolve(null, "DK"));
        Assert.Null(Bundled.Resolve("   ", "DK"));
    }

    [Fact]
    public void ResolveAll_MultiLocation_Returns_Every_Site()
    {
        var places = Bundled.ResolveAll("Copenhagen / Aarhus", "DK");
        Assert.Equal(2, places.Count);
        Assert.All(places, p => Assert.Equal("DK", p.CountryCode));
    }

    [Fact]
    public void Resolve_Ambiguous_Name_Prefers_Home_Country_Then_Population()
    {
        var g = Gazetteer.FromEntries(
        [
            new GazetteerEntry("Springfield", [], 39.80, -89.64, "US", GeoPlaceType.City, 200_000),
            new GazetteerEntry("Springfield", [], 55.00, 10.00, "DK", GeoPlaceType.City, 1_000),
        ]);

        Assert.Equal("DK", g.Resolve("Springfield", "DK")!.CountryCode);
        Assert.Equal("US", g.Resolve("Springfield", null)!.CountryCode);
        Assert.Equal("US", g.Resolve("Springfield", "SE")!.CountryCode);
    }

    [Fact]
    public void Parse_Rejects_Malformed_Rows()
    {
        Assert.Throws<Jobmatch.ConfigException>(() => Gazetteer.Parse("not-a-row"));
    }
}
