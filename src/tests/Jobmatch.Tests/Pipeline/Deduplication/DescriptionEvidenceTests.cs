using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Deduplication;
using Jobmatch.Pipeline.Geo;

namespace Jobmatch.Tests.Pipeline.Deduplication;

/// <summary>
/// Body-text evidence (R-117): near-copy descriptions can settle a pair the title and location
/// leave open — but never overrule two resolved, disagreeing places, because one employer's
/// reqs share template text.
/// </summary>
public sealed class DescriptionEvidenceTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København"], 55.67594, 12.56553, "DK", GeoPlaceType.City, 1_153_615),
        new GazetteerEntry("Hellerup", [], 55.73204, 12.57093, "DK", GeoPlaceType.City, 0),
        new GazetteerEntry("Manila", [], 14.60420, 120.98220, "PH", GeoPlaceType.City, 1_600_000),
    ]);

    private static readonly ProbabilisticMatcher Matcher = new(Gaz);

    private static readonly string AdText = string.Join(" ", Enumerable.Range(0, 400).Select(i =>
        $"word{i} responsibility{i % 7} delivering scalable dotnet services for the trading platform"));

    private static readonly string OtherText = string.Join(" ", Enumerable.Range(0, 400).Select(i =>
        $"different{i} vocabulary{i % 5} entirely about warehouse logistics and forklift certification"));

    private static Listing Make(
        string title, string? company, string? location, string description,
        string portal = "portal-a", string url = "https://a.com/1")
    {
        return new Listing(
            Id: $"{portal}-{url}",
            Portal: portal,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Unknown,
            Description: description,
            Url: new Uri(url),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    [Fact]
    public void NearCopy_Body_Settles_An_Unresolvable_Location()
    {
        // The Saxo Bank case: jobindex says "Hellerup", workday says "Headquarters (IT)" —
        // unresolvable, so the near-copy body is what proves the pair.
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Saxo Bank", "Hellerup", AdText),
            Make("Senior Frontend Developer", "Saxo Bank", "Headquarters (IT)", AdText,
                portal: "portal-b", url: "https://b.com/2"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
        Assert.True(verdict.DescriptionEvidence > 0);
    }

    [Fact]
    public void NearCopy_Body_Never_Overrules_A_Resolved_Location_Conflict()
    {
        // SimCorp's Manila and Copenhagen reqs share template text; the places disagree and win.
        var verdict = Matcher.Compare(
            Make("Lead AI Agent", "SimCorp", "København", AdText),
            Make("Lead AI Agent", "SimCorp", "Manila", AdText, portal: "portal-b", url: "https://b.com/2"));
        Assert.NotEqual(MatchBand.SameAd, verdict.Band);
        Assert.Equal(0, verdict.DescriptionEvidence);
    }

    [Fact]
    public void An_Excerpt_Contained_In_The_Full_Text_Counts_As_NearCopy()
    {
        var excerpt = string.Join(" ", AdText.Split(' ').Take(200));
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Saxo Bank", null, AdText),
            Make("Senior Frontend Developer", "Saxo Bank", null, excerpt, portal: "portal-b", url: "https://b.com/2"));
        Assert.True(verdict.DescriptionEvidence > 0);
    }

    [Fact]
    public void Tiny_Descriptions_Say_Nothing()
    {
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Saxo Bank", null, "Apply now!"),
            Make("Senior Frontend Developer", "Saxo Bank", null, AdText, portal: "portal-b", url: "https://b.com/2"));
        Assert.Equal(0, verdict.DescriptionEvidence);
    }

    [Fact]
    public void Substantial_Disjoint_Bodies_Argue_Against()
    {
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Saxo Bank", "Copenhagen", AdText),
            Make("Senior Frontend Developer", "Saxo Bank", "Copenhagen", OtherText,
                portal: "portal-b", url: "https://b.com/2"));
        Assert.True(verdict.DescriptionEvidence < 0);
    }

    [Fact]
    public void CompanyTokenSubset_Compares_And_Can_Merge()
    {
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Danske Bank", "Copenhagen", AdText),
            Make("Senior Frontend Developer", "Danske Bank Group", "København", AdText,
                portal: "portal-b", url: "https://b.com/2"));
        Assert.Equal(MatchBand.SameAd, verdict.Band);
    }

    [Fact]
    public void Shared_First_Token_Without_Subset_Is_Distinct()
    {
        var verdict = Matcher.Compare(
            Make("Senior Frontend Developer", "Danske Bank", "Copenhagen", AdText),
            Make("Senior Frontend Developer", "Danske Spil", "Copenhagen", AdText,
                portal: "portal-b", url: "https://b.com/2"));
        Assert.Equal(MatchBand.Distinct, verdict.Band);
    }
}
