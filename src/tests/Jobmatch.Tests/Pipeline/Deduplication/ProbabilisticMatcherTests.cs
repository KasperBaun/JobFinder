using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Search.Deduplication;
using Jobmatch.Search.Locations;

namespace Jobmatch.Tests.Pipeline.Deduplication;

public sealed class ProbabilisticMatcherTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København", "Kbh", "Cph"], 55.67594, 12.56553, "DK", GeoPlaceType.City, 1_153_615),
        new GazetteerEntry("København Ø", ["2100"], 55.70998, 12.57388, "DK", GeoPlaceType.Postal, 0),
        new GazetteerEntry("København V", ["1550"], 55.67244, 12.56124, "DK", GeoPlaceType.Postal, 0),
        new GazetteerEntry("Århus", ["Arhus", "Aarhus"], 56.15674, 10.21076, "DK", GeoPlaceType.City, 285_273),
        new GazetteerEntry("Berlin", [], 52.52437, 13.41053, "DE", GeoPlaceType.City, 3_426_354),
        new GazetteerEntry("Manila", [], 14.60420, 120.98220, "PH", GeoPlaceType.City, 1_600_000),
        new GazetteerEntry("Denmark", ["Danmark"], 56.0, 10.0, "DK", GeoPlaceType.Country, 5_800_000),
        new GazetteerEntry("France", ["Frankrig"], 46.0, 2.0, "FR", GeoPlaceType.Country, 67_000_000),
        new GazetteerEntry("Lithuania", ["Litauen"], 55.0, 24.0, "LT", GeoPlaceType.Country, 2_800_000),
    ]);

    private static readonly ProbabilisticMatcher Matcher = new(Gaz);

    private static Listing Make(
        string title, string? company, string? location = null, string url = "https://a.com/1",
        DateTimeOffset? postedAt = null, string portal = "portal-a")
    {
        return new Listing(
            Id: $"{title}-{url}",
            Portal: portal,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Unknown,
            Description: string.Empty,
            Url: new Uri(url),
            PostedAt: postedAt,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    [Fact]
    public void Compare_DifferentCompanies_Are_Distinct_Regardless_Of_Title()
    {
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer", "Acme", "Copenhagen"),
            Make("Senior Software Engineer", "Globex", "Copenhagen"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }

    [Fact]
    public void Compare_MissingCompany_Is_Distinct_Not_A_Wildcard()
    {
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer", null, "Copenhagen"),
            Make("Senior Software Engineer", null, "Copenhagen"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }

    [Fact]
    public void Compare_ExactTitle_SameCity_Is_SameAd()
    {
        // The Danske Bank pair: identical title, EN vs DA spelling of the same city.
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer C#/.net", "Danske Bank", "Copenhagen V, Denmark"),
            Make("Senior Software Engineer C#/.net", "Danske Bank A/S", "København", portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_SeniorityQualified_Title_With_Missing_Location_Is_SameAd()
    {
        // The pair T-012 phase 1 could not touch: jobindex renders the workday ad with a
        // "Senior/Lead" prefix and no location. Same ad — the subset seniority is no conflict.
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer- (C#, APL) Valuation Product Area", "SimCorp", "Copenhagen"),
            Make("Senior/Lead Software Engineer- (C#, APL) Valuation Product Area", "SimCorp A/S", null, portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_Senior_Vs_Lead_Is_Distinct_Despite_NearIdentical_Titles()
    {
        var verdict = Matcher.Compare(
            Make("Senior Full-Stack Software Engineer (.Net/Angular)", "SimCorp", "Copenhagen"),
            Make("Lead Full-Stack Software Engineer (.Net/Angular)", "SimCorp", "Copenhagen"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }

    [Fact]
    public void Compare_ExactTitle_Different_Cities_Is_At_Most_Possible()
    {
        // Wolt posts the same role per city — identical titles, genuinely different listings.
        var verdict = Matcher.Compare(
            Make("Grocery Associate", "Wolt", "Copenhagen"),
            Make("Grocery Associate", "Wolt", "Berlin"));
        Assert.NotEqual(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_ExactTitle_Missing_Location_Is_SameAd()
    {
        var verdict = Matcher.Compare(
            Make("Platform Engineer", "Acme", "Copenhagen"),
            Make("Platform Engineer", "Acme", null, portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_CountryLevel_Location_Is_Compatible_With_A_City_In_It()
    {
        // Run-4 audit: Jyske Bank's own site says "Denmark", jobindex says "København V" —
        // a granularity difference, not a conflict.
        var verdict = Matcher.Compare(
            Make("Cloud Sikkerhedsarkitekt", "Jyske Bank", "København V"),
            Make("Cloud Sikkerhedsarkitekt", "Jyske Bank", "Denmark", "https://b.com/2", portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
        Assert.Equal(0, verdict.LocationEvidence);
    }

    [Fact]
    public void Compare_MultiSite_List_Is_Compatible_With_One_Of_Its_Sites()
    {
        // Run-4 audit: jobindex re-lists the Danske Bank ad as "Indien, Litauen, Aarhus";
        // the source req says "Aarhus C, Denmark".
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer - Cloud Archive", "Danske Bank", "Indien, Litauen, Aarhus"),
            Make("Senior Software Engineer - Cloud Archive", "Danske Bank", "Aarhus C, Denmark", "https://b.com/2", portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_Nearby_Postal_And_City_Are_Compatible()
    {
        // Run-4 audit: cBrain's site says "Nordhavn, København Ø, Danmark", jobindex "København Ø".
        var verdict = Matcher.Compare(
            Make("Solution Developer", "cBrain", "Nordhavn, København Ø, Danmark"),
            Make("Solution Developer", "cBrain", "København Ø", "https://b.com/2", portal: "portal-b"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_Genuinely_Conflicting_Cities_Still_Penalise()
    {
        // Run-4 audit: SimCorp's Manila req is not the Copenhagen ad, whatever the title says.
        var verdict = Matcher.Compare(
            Make("Lead AI Agent", "SimCorp", "København"),
            Make("Lead AI Agent", "SimCorp", "Manila", "https://b.com/2", portal: "portal-b"));
        Assert.NotEqual(MatchBand.SameAd, verdict.Band);
        Assert.True(verdict.LocationEvidence < 0);
    }

    [Fact]
    public void Compare_Foreign_Country_Claim_Conflicts_With_A_Danish_City()
    {
        // Run-4 audit: jobindex tagged the Danske Bank ad "Frankrig"; oracle says Copenhagen.
        var verdict = Matcher.Compare(
            Make("AI Engineer for Agent Development", "Danske Bank", "Frankrig"),
            Make("AI Engineer for Agent Development", "Danske Bank", "Copenhagen V, Denmark", "https://b.com/2", portal: "portal-b"));
        Assert.NotEqual(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_Unresolvable_Location_Text_Still_Conflicts()
    {
        // "Headquarters (IT)" resolves to nothing — compatibility cannot be established.
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Saxo Bank", "Hellerup"),
            Make("Senior Frontend Developer", "Saxo Bank", "Headquarters (IT)", "https://b.com/2", portal: "portal-b"));
        Assert.NotEqual(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Compare_SamePortal_Pair_Caps_At_Possible()
    {
        // Two distinct URLs on one source are two reqs — "Senior X" and "X" on the employer's
        // own ATS. The exact-key deduper already merged true same-portal duplicates by URL.
        var verdict = Matcher.Compare(
            Make("Senior .NET Software Engineer for Markets Post-Trade Technology Tribe", "Danske Bank", "Vilnius"),
            Make(".NET Software Engineer for Markets Post-Trade Technology Tribe", "Danske Bank", "Vilnius", "https://a.com/2"));
        Assert.Equal(MatchBand.Possible, verdict.Band);
    }

    [Fact]
    public void Compare_Diverging_Stacks_Are_Distinct_Despite_Wordy_Title_Overlap()
    {
        // The Aug 6 near-miss: Danish filler tokens (til, afdeling, i, vækst) inflated Jaccard
        // to p=0.89 for a .Net role vs a Java role. The stack guard sinks it outright.
        var verdict = Matcher.Compare(
            Make("Senior .Net udvikler til afdeling i vækst", "Sopra Steria", "København", portal: "portal-a"),
            Make("Senior Fullstack Java udvikler til afdeling i vækst", "Sopra Steria", "København", "https://b.com/2", portal: "portal-b"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }

    [Fact]
    public void Compare_CSharp_And_DotNet_Are_The_Same_Family()
    {
        // C#/.NET is one family, so no stack penalty applies — unlike C#/Java, where it does.
        // (The pair still lands Distinct on its own: a three-word title differing by a token is
        // genuinely weak evidence; what matters is the guard not firing on synonyms.)
        var sameFamily = Matcher.Compare(
            Make("Senior C# Developer", "Acme", "Copenhagen"),
            Make("Senior .NET Developer", "Acme", "Copenhagen", "https://b.com/2", portal: "portal-b"));
        var crossFamily = Matcher.Compare(
            Make("Senior C# Developer", "Acme", "Copenhagen"),
            Make("Senior Java Developer", "Acme", "Copenhagen", "https://c.com/3", portal: "portal-b"));
        Assert.True(sameFamily.TitleEvidence > crossFamily.TitleEvidence);
    }

    [Fact]
    public void Compare_Unrelated_Titles_Same_Company_Is_Distinct()
    {
        var verdict = Matcher.Compare(
            Make("Senior Software Engineer", "Acme", "Copenhagen"),
            Make("Head of People & Culture", "Acme", "Copenhagen"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }

    [Fact]
    public void Compare_Far_Apart_Posting_Dates_Weaken_The_Verdict()
    {
        var recent = Make("Platform Engineer", "Acme", null, postedAt: DateTimeOffset.UtcNow);
        var stale = Make("Platform Engineer", "Acme", null, "https://b.com/2",
            postedAt: DateTimeOffset.UtcNow.AddDays(-90));
        var close = Make("Platform Engineer", "Acme", null, "https://c.com/3",
            postedAt: DateTimeOffset.UtcNow.AddDays(-3));

        Assert.True(Matcher.Compare(recent, close).Probability > Matcher.Compare(recent, stale).Probability);
    }

    [Fact]
    public void Compare_Is_Symmetric()
    {
        var a = Make("Senior Software Engineer C#/.net", "Danske Bank", "Copenhagen");
        var b = Make("Senior/Lead Software Engineer C#/.net", "Danske Bank", null);
        Assert.Equal(Matcher.Compare(a, b), Matcher.Compare(b, a));
    }

    [Fact]
    public void Compare_Verdict_Carries_Field_Evidence()
    {
        var verdict = Matcher.Compare(
            Make("Platform Engineer", "Acme", "Copenhagen"),
            Make("Platform Engineer", "Acme", "Berlin"));
        Assert.True(verdict.TitleEvidence > 0);
        Assert.True(verdict.LocationEvidence < 0);
        Assert.Equal(0, verdict.RecencyEvidence);
    }
}
