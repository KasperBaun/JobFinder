using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Deduplication;
using Jobmatch.Pipeline.Geo;

namespace Jobmatch.Tests.Pipeline.Deduplication;

/// <summary>
/// The T-012 dedupe strengthening: gazetteer-canonical location keys, HTML-entity
/// normalisation, and key registration for absorbed listings. The cross-portal cases
/// replicate the pairs that survived to the top-20 of run 20260806-113247-dd3dc6.
/// </summary>
public sealed class DeduperGazetteerTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København", "Kbh", "Cph"], 55.67594, 12.56553, "DK", GeoPlaceType.City, 1_153_615),
        new GazetteerEntry("Århus", ["Arhus", "Aarhus"], 56.15674, 10.21076, "DK", GeoPlaceType.City, 285_273),
        new GazetteerEntry("Aalborg", [], 57.04880, 9.92170, "DK", GeoPlaceType.City, 120_000),
        new GazetteerEntry("Berlin", [], 52.52437, 13.41053, "DE", GeoPlaceType.City, 3_426_354),
    ]);

    private static Listing Make(string portal, string title, string? company, string url, string? location = null)
    {
        return new Listing(
            Id: $"{portal}-{title}-{url}",
            Portal: portal,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Unknown,
            Description: string.Empty,
            Url: new Uri(url),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    [Fact]
    public void Deduplicate_CrossPortal_EnglishAndDanishCityName_Collapse_With_Gazetteer()
    {
        // The run's #1 and #2: oracle says "Copenhagen V, Denmark", jobindex says "København".
        var oracle = Make("oracle-danskebank",
            title: "Senior Software Engineer C#/.net",
            company: "Danske Bank",
            url: "https://eeho.fa.us2.oraclecloud.com/hcmUI/CandidateExperience/job/JR123",
            location: "Copenhagen V, Denmark");
        var jobindex = Make("jobindex-rss-csharp",
            title: "Senior Software Engineer C#/.net",
            company: "Danske Bank",
            url: "https://www.jobindex.dk/vis-job/h1650000",
            location: "København");
        Assert.Single(Deduper.Deduplicate([oracle, jobindex], Gaz).Deduped);
    }

    [Fact]
    public void Deduplicate_EnglishAndDanishCityName_Stay_Distinct_Without_Gazetteer()
    {
        var oracle = Make("a", "Senior Engineer", "Acme", "https://a.com/1", location: "Copenhagen V, Denmark");
        var jobindex = Make("b", "Senior Engineer", "Acme", "https://b.com/1", location: "København");
        Assert.Equal(2, Deduper.Deduplicate([oracle, jobindex]).Deduped.Count);
    }

    [Fact]
    public void Deduplicate_AarhusSpellings_Collapse_With_Gazetteer()
    {
        var input = new[]
        {
            Make("a", "Platform Engineer", "Acme", "https://a.com/1", location: "Århus"),
            Make("b", "Platform Engineer", "Acme", "https://b.com/1", location: "Aarhus C"),
        };
        Assert.Single(Deduper.Deduplicate(input, Gaz).Deduped);
    }

    [Fact]
    public void Deduplicate_Different_Resolved_Cities_Stay_Distinct()
    {
        var input = new[]
        {
            Make("a", "Senior Engineer", "Acme", "https://a.com/1", location: "Copenhagen"),
            Make("b", "Senior Engineer", "Acme", "https://b.com/1", location: "Berlin"),
        };
        Assert.Equal(2, Deduper.Deduplicate(input, Gaz).Deduped.Count);
    }

    [Fact]
    public void Deduplicate_SeniorAndLead_Variants_Of_Same_Role_Stay_Distinct()
    {
        // SimCorp genuinely posts both — near-identical titles are two real jobs, and the
        // deduper must never fuzzy-match titles into a destructive merge.
        var senior = Make("workday-simcorp",
            title: "Senior Full-Stack Software Engineer (.Net/Angular)",
            company: "SimCorp",
            url: "https://simcorp.wd3.myworkdayjobs.com/jobs/job/Senior-Software-Engineer_R-1",
            location: "Copenhagen");
        var lead = Make("workday-simcorp",
            title: "Lead Full-Stack Software Engineer (.Net/Angular)",
            company: "SimCorp",
            url: "https://simcorp.wd3.myworkdayjobs.com/jobs/job/Lead-Software-Engineer_R-2",
            location: "Copenhagen");
        Assert.Equal(2, Deduper.Deduplicate([senior, lead], Gaz).Deduped.Count);
    }

    [Theory]
    [InlineData("Fullstack Developer (.NET &amp; Angular)", "Fullstack Developer (.NET & Angular)")]
    [InlineData("Fullstack Developer (.NET &amp;amp; Angular)", "Fullstack Developer (.NET & Angular)")]
    [InlineData("Work &amp; Security", "Work & Security")]
    public void Deduplicate_EncodedAndDecodedTitles_Collapse(string encoded, string decoded)
    {
        var input = new[]
        {
            Make("a", encoded, "Acme", "https://a.com/1", location: "Copenhagen"),
            Make("b", decoded, "Acme", "https://b.com/1", location: "Copenhagen"),
        };
        Assert.Single(Deduper.Deduplicate(input).Deduped);
    }

    [Fact]
    public void Deduplicate_UrlAbsorbed_Listing_Still_Registers_Its_Title_Key()
    {
        // B collapses into A by URL; C carries B's spelling of the title on a third URL.
        // Without key registration C would survive as a phantom third copy.
        var a = Make("portal-a", "Job A", "Acme", "https://acme.com/jobs/1");
        var b = Make("portal-b", "Job A (variant)", "Acme", "https://acme.com/jobs/1#apply");
        var c = Make("portal-c", "Job A (variant)", "Acme", "https://other.com/listing/x");

        var result = Deduper.Deduplicate([a, b, c]);

        Assert.Single(result.Deduped);
        var group = Assert.Single(result.Merges);
        Assert.Equal(a.Id, group.CanonicalId);
        Assert.Equal(2, group.MergedFromIds.Count);
    }

    [Fact]
    public void Deduplicate_TclAbsorbed_Listing_Still_Registers_Its_Url_Key()
    {
        // B collapses into A by title/company/location; C repeats B's URL.
        var a = Make("portal-a", "Job A", "Acme", "https://acme.com/jobs/1");
        var b = Make("portal-b", "Job A", "Acme", "https://mirror.com/jobs/9");
        var c = Make("portal-c", "Job A (retitled)", "Acme", "https://mirror.com/jobs/9");

        var result = Deduper.Deduplicate([a, b, c]);

        Assert.Single(result.Deduped);
        var group = Assert.Single(result.Merges);
        Assert.Equal(a.Id, group.CanonicalId);
        Assert.Equal(2, group.MergedFromIds.Count);
    }

    [Fact]
    public void Deduplicate_CommaSeparated_MultiSite_List_Never_Shares_A_Key_With_One_Of_Its_Sites()
    {
        // Caught live (T-013 run-6 audit): Wolt's Aalborg-only posting exact-merged with its
        // five-city posting because the first-comma cut reduced both keys to "aalborg".
        var single = Make("wolt", "Grocery Associate (under 18 years old)", "Wolt",
            "https://wolt.com/jobs/6884794", location: "Aalborg, Denmark");
        var multi = Make("wolt", "Grocery Associate (under 18 years old)", "Wolt",
            "https://wolt.com/jobs/6693554",
            location: "Aalborg, Denmark; Aarhus, Denmark; Copenhagen, Denmark");

        Assert.Equal(2, Deduper.Deduplicate([single, multi], Gaz).Deduped.Count);
    }

    [Fact]
    public void Deduplicate_Identical_MultiSite_Lists_Still_Merge_Regardless_Of_Order()
    {
        var a = Make("a", "Grocery Associate", "Wolt", "https://a.com/1",
            location: "Aalborg, Denmark; Copenhagen, Denmark");
        var b = Make("b", "Grocery Associate", "Wolt", "https://b.com/1",
            location: "Copenhagen, Denmark; Aalborg, Denmark");

        Assert.Single(Deduper.Deduplicate([a, b], Gaz).Deduped);
    }

    [Fact]
    public void NormaliseLocation_SingleSite_CityCountry_Still_Takes_The_Reduced_Path()
    {
        // "Copenhagen V, Denmark" must keep reducing to the bare city — the multi-site guard
        // must not see the country suffix as a second site.
        Assert.Equal(
            Deduper.NormaliseLocation("København", Gaz),
            Deduper.NormaliseLocation("Copenhagen, Denmark", Gaz));
    }

    [Fact]
    public void NormaliseLocation_BundledGazetteer_Makes_RealWorld_Spellings_Meet()
    {
        var bundled = Gazetteer.LoadBundled();
        var oracle = Deduper.NormaliseLocation("Copenhagen V, Denmark", bundled);
        var jobindex = Deduper.NormaliseLocation("København", bundled);
        Assert.Equal(oracle, jobindex);
        // The " #cc" suffix proves the key came from gazetteer resolution, not string luck.
        Assert.EndsWith(" #dk", oracle);
        Assert.Equal(
            Deduper.NormaliseLocation("Århus", bundled),
            Deduper.NormaliseLocation("Aarhus C", bundled));
        Assert.NotEqual(oracle, Deduper.NormaliseLocation("Berlin", bundled));
    }

    [Fact]
    public void Deduplicate_MultiSite_Listings_Match_On_The_Site_Set_Not_Its_Order()
    {
        var input = new[]
        {
            Make("a", "Senior Engineer", "Acme", "https://a.com/1", location: "Copenhagen / Berlin"),
            Make("b", "Senior Engineer", "Acme", "https://b.com/1", location: "Berlin / Copenhagen"),
        };
        Assert.Single(Deduper.Deduplicate(input, Gaz).Deduped);
    }

    [Fact]
    public void Deduplicate_MultiSite_Listing_Does_Not_Absorb_A_SingleSite_One()
    {
        // "Copenhagen / Berlin" and a bare "Copenhagen" may be two postings of the same role or
        // two roles — a destructive merge must not gamble on it.
        var input = new[]
        {
            Make("a", "Senior Engineer", "Acme", "https://a.com/1", location: "Copenhagen / Berlin"),
            Make("b", "Senior Engineer", "Acme", "https://b.com/1", location: "Copenhagen"),
        };
        Assert.Equal(2, Deduper.Deduplicate(input, Gaz).Deduped.Count);
    }

    [Fact]
    public void NormaliseLocation_Unresolved_Falls_Back_To_String_Normalisation()
    {
        Assert.Equal("some obscure place", Deduper.NormaliseLocation("Some  Obscure Place", Gaz));
        Assert.Equal(string.Empty, Deduper.NormaliseLocation(null, Gaz));
    }
}
