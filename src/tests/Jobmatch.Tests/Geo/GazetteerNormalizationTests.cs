using Jobmatch.Geo;

namespace Jobmatch.Tests.Geo;

/// <summary>
/// How a raw location field is cleaned before it is looked up. Every case here comes from a
/// shape seen in real ATS output: bracketed qualifiers, non-breaking and zero-width spaces,
/// trailing punctuation, and the separators employers use to list several sites.
/// </summary>
public sealed class GazetteerNormalizationTests
{
    private static readonly Gazetteer Bundled = Gazetteer.LoadBundled();

    private static IReadOnlyList<GeoPlace> Sites(string location) => Bundled.ResolveSites(location, "DK");

    private static bool NearCopenhagen(GeoPlace p) =>
        p.CountryCode == "DK" && p.Latitude is > 55.55 and < 55.80 && p.Longitude is > 12.4 and < 12.7;

    [Theory]
    [InlineData("Copenhagen (Primary), Aarhus")]
    [InlineData("Aarhus, [Copenhagen]")]
    [InlineData("Aarhus, København (hovedkontor)")]
    [InlineData("Aarhus, Copenhagen:")]
    public void BracketedAndTrailingQualifiers_Do_Not_Hide_A_Site(string location)
    {
        Assert.Contains(Sites(location), NearCopenhagen);
    }

    [Fact]
    public void A_Name_That_Contains_Brackets_Still_Matches_On_Its_Own_Terms()
    {
        var place = Bundled.Resolve("Halle (Saale)", "DK");
        Assert.NotNull(place);
        Assert.Equal("DE", place!.CountryCode);
    }

    [Theory]
    [InlineData("København K")]      // non-breaking space between the words
    [InlineData("København K​")]     // trailing zero-width space
    [InlineData("﻿København K")]     // leading BOM
    public void Exotic_Whitespace_Does_Not_Break_A_Name(string location)
    {
        Assert.Contains(Sites(location), NearCopenhagen);
    }

    [Theory]
    [InlineData("Copenhagen | Aarhus")]
    [InlineData("København – Aarhus")]
    [InlineData("Aarhus-København")]
    public void Pipes_And_Dashes_Separate_Sites(string location)
    {
        var sites = Sites(location);
        Assert.Contains(sites, NearCopenhagen);
        Assert.Contains(sites, p => p.CountryCode == "DK" && p.Latitude > 56.0);
    }

    // The index answers to two-letter country codes, so a split has to ignore fragments that
    // short — otherwise "Île-de-France" reads as Germany and "Gyeonggi-do" as the Dominican
    // Republic, and the drop message names a country the listing never mentioned.
    [Theory]
    [InlineData("Lieusaint, Île-de-France, France", "FR")]
    [InlineData("South Korea, Gyeonggi-do, Ichon, 17389", "KR")]
    [InlineData("IT og Support, Aarhus", "DK")]
    public void Short_Fragments_Of_A_Split_Are_Not_Country_Codes(string location, string expected)
    {
        var sites = Sites(location);
        Assert.NotEmpty(sites);
        Assert.All(sites, p => Assert.Equal(expected, p.CountryCode));
    }

    // …while a whole segment that *is* a country code still counts.
    [Fact]
    public void A_Whole_Segment_May_Still_Be_A_Country_Code()
    {
        var sites = Sites("Aarhus C, DK, 8000");
        Assert.NotEmpty(sites);
        Assert.All(sites, p => Assert.Equal("DK", p.CountryCode));
    }
}
