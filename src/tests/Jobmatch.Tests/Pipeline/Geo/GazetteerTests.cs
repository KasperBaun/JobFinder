using Jobmatch.Pipeline.Geo;

namespace Jobmatch.Tests.Pipeline.Geo;

public sealed class GazetteerTests
{
    // The real bundled TSV — also proves the csproj bundling copies it next to the binaries.
    private static readonly Gazetteer Bundled = Gazetteer.LoadBundled();

    private static IReadOnlyList<GeoPlace> Sites(string location) => Bundled.ResolveSites(location, "DK");

    private static bool NearCopenhagen(GeoPlace p) =>
        p.CountryCode == "DK" && p.Latitude is > 55.55 and < 55.80 && p.Longitude is > 12.4 and < 12.7;

    private static bool NearAarhus(GeoPlace p) =>
        p.CountryCode == "DK" && p.Latitude is > 56.1 and < 56.2;

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

    [Fact]
    public void ResolveSites_TrailingCountry_Is_A_Qualifier_Not_A_Second_Site()
    {
        var site = Assert.Single(Sites("Aarhus, Denmark"));
        Assert.Equal(GeoPlaceType.City, site.Type);
        Assert.True(NearAarhus(site));
    }

    [Fact]
    public void ResolveSites_Conjunction_Splits_A_Segment_Into_Several_Sites()
    {
        var sites = Sites("Copenhagen, Aarhus or Aalborg, , Denmark");
        Assert.Contains(sites, NearCopenhagen);
        Assert.Contains(sites, NearAarhus);
        // Aalborg arrives as a postal row and Copenhagen as a city — both count as precise.
        Assert.Contains(sites, p => p.Type == GeoPlaceType.Postal);
        Assert.Contains(sites, p => p.Type == GeoPlaceType.City);
        Assert.DoesNotContain(sites, p => p.Type is GeoPlaceType.Region or GeoPlaceType.Country);
    }

    [Theory]
    [InlineData("Silkeborg, Roskilde og mulighed for hjemmearbejde", "Roskilde")]
    [InlineData("Silkeborg, Roskilde og mulighed for hjemmearbejde", "Silkeborg")]
    [InlineData("København V med mulighed for hjemmearbejde", "København V")]
    [InlineData("Aalborg, Vejle, Glostrup & København", "Glostrup")]
    [InlineData("Aalborg, Vejle, Glostrup & København", "Copenhagen")]
    public void ResolveSites_Conjunction_Splits_Sites_From_Prose(string location, string expected)
    {
        Assert.Contains(Sites(location), p => p.Name == expected);
    }

    [Theory]
    [InlineData("Copenhagen / Aarhus")]
    [InlineData("Chicago; San Francisco")]
    [InlineData("Copenhagen + Aarhus")]
    public void ResolveSites_Separators_And_Conjunctions_Yield_Both_Sites(string location)
    {
        Assert.Equal(2, Sites(location).Count);
    }

    [Theory]
    [InlineData("Nordjylland, Danmark", "Region Nordjylland")]
    [InlineData("Region Syddanmark, Danmark", "Region Syddanmark")]
    public void ResolveSites_Region_Outranks_The_Country_Beside_It(string location, string expected)
    {
        var site = Assert.Single(Sites(location));
        Assert.Equal(GeoPlaceType.Region, site.Type);
        Assert.Equal(expected, site.Name);
    }

    [Fact]
    public void ResolveSites_CountryOnly_Falls_Back_To_The_Country()
    {
        var site = Assert.Single(Sites("Poland"));
        Assert.Equal(GeoPlaceType.Country, site.Type);
        Assert.Equal("PL", site.CountryCode);
    }

    [Fact]
    public void ResolveAll_ConjunctionSplit_Keeps_MultiWord_Country_Names_Whole()
    {
        var place = Assert.Single(Bundled.ResolveAll("Trinidad and Tobago", "DK"));
        Assert.Equal(GeoPlaceType.Country, place.Type);
        Assert.Equal("TT", place.CountryCode);
    }

    [Theory]
    [InlineData("Philippines, Pasig, 1600")]
    [InlineData("USA, Virginia, Norfolk, 23510-3300")]
    public void ResolveAll_ForeignAddress_Does_Not_Borrow_A_Danish_PostalCode(string location)
    {
        var places = Bundled.ResolveAll(location, "DK");
        Assert.NotEmpty(places);
        Assert.All(places, p => Assert.NotEqual("DK", p.CountryCode));
    }

    [Theory]
    [InlineData("23510-3300")]
    [InlineData("2770-131")]
    [InlineData("dk2800")]
    [InlineData("H2020")]
    public void ResolveAll_FourDigits_That_Do_Not_Stand_Alone_Are_Not_Postal_Codes(string location)
    {
        Assert.Empty(Bundled.ResolveAll(location, "DK"));
    }

    [Fact]
    public void ResolveSites_ForeignPostal_Yields_The_Foreign_City()
    {
        var sites = Sites("Antwerpen 2000, Belgium");
        Assert.All(sites, p => Assert.NotEqual("DK", p.CountryCode));
        Assert.Contains(sites, p => p.CountryCode == "BE" && p.Latitude is > 51.1 and < 51.3);
    }

    [Fact]
    public void Resolve_Strips_A_Standalone_FourDigit_Token_Before_The_Name_Lookup()
    {
        var place = Bundled.Resolve("Wien 1010", "DK");
        Assert.NotNull(place);
        Assert.Equal("AT", place!.CountryCode);
        Assert.InRange(place.Latitude, 48.1, 48.3);
    }

    [Fact]
    public void Resolve_BareDanishPostalCode_Needs_No_Name()
    {
        var place = Bundled.Resolve("2300", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Postal, place!.Type);
        Assert.Equal("København S", place.Name);
    }

    [Theory]
    [InlineData("DK-2300", "København S")]
    [InlineData("DK-2800 Kgs. Lyngby", "Kongens Lyngby")]
    [InlineData("DK-9000 Aalborg", "Aalborg")]
    [InlineData("Vestre Ringgade 1, DK-8000 Aarhus C", "Aarhus C")]
    public void Resolve_DkPrefixed_PostalCode_Still_Resolves(string location, string expected)
    {
        var place = Bundled.Resolve(location, "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Postal, place!.Type);
        Assert.Equal(expected, place.Name);
    }

    // 232 rows share the name "København K"; only the 1433 row sits out on Refshaleøen.
    [Fact]
    public void Resolve_PostalCode_Wins_Over_A_Namesake_Row_In_The_Same_Segment()
    {
        var place = Bundled.Resolve("1433 København K", "DK");
        Assert.NotNull(place);
        Assert.Equal(GeoPlaceType.Postal, place!.Type);
        Assert.InRange(place.Latitude, 55.72, 55.74);
        Assert.InRange(place.Longitude, 12.71, 12.74);
    }

    [Fact]
    public void ResolveSites_DanishQualifier_Lets_A_Bare_PostalCode_Count()
    {
        var site = Assert.Single(Sites("Danmark, 2300"));
        Assert.Equal(GeoPlaceType.Postal, site.Type);
        Assert.True(NearCopenhagen(site));
    }
}
