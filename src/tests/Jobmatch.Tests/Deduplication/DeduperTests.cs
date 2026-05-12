using System.Text.Json;
using Jobmatch.Deduplication;
using Jobmatch.Models;

namespace Jobmatch.Tests.Deduplication;

public sealed class DeduperTests
{
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
    public void Deduplicate_IdenticalUrl_Collapses_To_One()
    {
        var input = new[]
        {
            Make("a", "Software Engineer", "Acme", "https://acme.com/jobs/1"),
            Make("b", "Software Engineer", "Acme", "https://acme.com/jobs/1"),
        };
        var result = Deduper.Deduplicate(input).Deduped;
        Assert.Single(result);
    }

    [Fact]
    public void Deduplicate_Url_Differs_By_Fragment_Only_Are_Dupes()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/jobs/1"),
            Make("a", "Job A (variant)", "Acme", "https://acme.com/jobs/1#apply"),
        };
        Assert.Single(Deduper.Deduplicate(input).Deduped);
    }

    [Fact]
    public void Deduplicate_Url_Differs_By_Trailing_Slash_Are_Dupes()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/jobs/1"),
            Make("b", "Job A", "Acme", "https://acme.com/jobs/1/"),
        };
        Assert.Single(Deduper.Deduplicate(input).Deduped);
    }

    [Fact]
    public void Deduplicate_Same_Title_And_Company_Different_Urls_Are_Dupes()
    {
        var input = new[]
        {
            Make("linkedin", "Senior Engineer", "Acme Corp", "https://linkedin.com/jobs/1"),
            Make("jobnet", "Senior Engineer", "Acme Corp", "https://jobnet.dk/jobs/abc"),
        };
        Assert.Single(Deduper.Deduplicate(input).Deduped);
    }

    [Fact]
    public void Deduplicate_Case_And_Whitespace_Differences_Still_Match()
    {
        var input = new[]
        {
            Make("a", "Senior   Engineer", "Acme  Corp", "https://a.com/1"),
            Make("b", "senior engineer", "ACME CORP", "https://b.com/2"),
        };
        Assert.Single(Deduper.Deduplicate(input).Deduped);
    }

    [Fact]
    public void Deduplicate_Same_Title_Company_Different_Location_Are_Distinct()
    {
        // Acme hiring Senior Engineer in both Copenhagen and Berlin — two legit distinct roles.
        var input = new[]
        {
            Make("jobnet", "Senior Engineer", "Acme", "https://acme.com/jobs/cph", location: "Copenhagen"),
            Make("jobnet", "Senior Engineer", "Acme", "https://acme.com/jobs/ber", location: "Berlin"),
        };
        Assert.Equal(2, Deduper.Deduplicate(input).Deduped.Count);
    }

    [Fact]
    public void Deduplicate_Different_Companies_Are_Distinct()
    {
        var input = new[]
        {
            Make("a", "Senior Engineer", "Acme", "https://a.com/1"),
            Make("b", "Senior Engineer", "Globex", "https://b.com/1"),
        };
        Assert.Equal(2, Deduper.Deduplicate(input).Deduped.Count);
    }

    [Fact]
    public void Deduplicate_Preserves_Order_Keeping_First_Occurrence()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/1"),
            Make("a", "Job B", "Acme", "https://acme.com/2"),
            Make("b", "Job A", "Acme", "https://other.com/1"),
        };
        var result = Deduper.Deduplicate(input).Deduped;
        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0].Portal);
        Assert.Equal("Job A", result[0].Title);
        Assert.Equal("Job B", result[1].Title);
    }

    [Fact]
    public void Deduplicate_MergeGroups_Cover_Each_Duplicate_Exactly_Once()
    {
        var firstA = Make("portal-a", "Senior Engineer", "Acme", "https://acme.com/jobs/1");
        var dupeUrlA = Make("portal-b", "Senior Engineer", "Acme", "https://acme.com/jobs/1#section");
        var dupeTclA = Make("portal-c", "Senior Engineer", "Acme", "https://other.com/listing/abc");
        var distinct = Make("portal-d", "Junior Engineer", "Acme", "https://acme.com/jobs/2");

        var result = Deduper.Deduplicate([firstA, dupeUrlA, dupeTclA, distinct]);

        Assert.Equal(2, result.Deduped.Count);
        Assert.Single(result.Merges);

        var group = result.Merges[0];
        Assert.Equal(firstA.Id, group.CanonicalId);
        Assert.Equal(2, group.MergedFromIds.Count);
        Assert.Contains(dupeUrlA.Id, group.MergedFromIds);
        Assert.Contains(dupeTclA.Id, group.MergedFromIds);
    }

    [Fact]
    public void Deduplicate_NoDuplicates_Yields_Empty_Merges()
    {
        var input = new[]
        {
            Make("a", "Engineer", "Acme", "https://a.com/1"),
            Make("b", "Engineer", "Globex", "https://b.com/1"),
        };
        var result = Deduper.Deduplicate(input);
        Assert.Equal(2, result.Deduped.Count);
        Assert.Empty(result.Merges);
    }

    [Theory]
    [InlineData("Sopra Steria A/S", "Sopra Steria")]
    [InlineData("Sopra Steria ApS", "Sopra Steria")]
    [InlineData("Danske Spil A/S", "Danske Spil")]
    [InlineData("ACME GmbH", "ACME")]
    [InlineData("Acme, Inc.", "Acme")]
    [InlineData("Plain Company", "Plain Company")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormaliseCompany_StripsLegalForm(string? input, string expectedRaw)
    {
        Assert.Equal(Deduper.Normalise(expectedRaw), Deduper.NormaliseCompany(input));
    }

    [Theory]
    [InlineData("København K og mulighed for hjemmearbejde", "københavn")]
    [InlineData("Brøndby og mulighed for hjemmearbejde", "brøndby")]
    [InlineData("Herlev og mulighed for fjernarbejde", "herlev")]
    [InlineData("København, , Denmark", "københavn")]
    [InlineData("Brøndby, Denmark", "brøndby")]
    [InlineData("København Ø", "københavn")]
    [InlineData("Copenhagen", "copenhagen")]
    [InlineData("New York, NY", "new york")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormaliseLocation_StripsRemoteSuffixCommaSegmentsAndDistrictLetter(string? input, string expected)
    {
        Assert.Equal(expected, Deduper.NormaliseLocation(input));
    }

    [Fact]
    public void Deduplicate_CrossPortal_JobindexAndSmartRecruiters_Collapse_When_LegalFormAndDistrictDiffer()
    {
        // The Jobindex side already had its trailing ", Sopra Steria A/S" split into the
        // company field by RssAdapter — we simulate that post-extraction state here.
        var jobindex = Make("jobindex-rss-net-udvikler",
            title: "Senior .Net udvikler til afdeling i vækst",
            company: "Sopra Steria A/S",
            url: "https://www.jobindex.dk/vis-job/h1646496",
            location: "København K og mulighed for hjemmearbejde");
        var smartrecruiters = Make("smartrecruiters-soprasteria",
            title: "Senior .Net udvikler til afdeling i vækst",
            company: "Sopra Steria",
            url: "https://jobs.smartrecruiters.com/SopraSteria1/744000113431214",
            location: "København, , Denmark");
        Assert.Single(Deduper.Deduplicate([jobindex, smartrecruiters]).Deduped);
    }

    [Fact]
    public void Deduplicate_CrossPortal_JobindexAndTeamtailor_Collapse_When_LegalFormAndRemoteSuffixDiffer()
    {
        var jobindex = Make("jobindex-rss-net-udvikler",
            title: "Softwareudvikler – byg fundamentet for Danske Spils digitale platform med AI-first udvikling",
            company: "Danske Spil A/S",
            url: "https://www.jobindex.dk/vis-job/h1662925",
            location: "Brøndby og mulighed for hjemmearbejde");
        var teamtailor = Make("teamtailor-danske-spil",
            title: "Softwareudvikler – byg fundamentet for Danske Spils digitale platform med AI-first udvikling",
            company: "Danske Spil",
            url: "https://karriere.danskespil.dk/jobs/7690844",
            location: "Brøndby, Denmark");
        Assert.Single(Deduper.Deduplicate([jobindex, teamtailor]).Deduped);
    }
}
