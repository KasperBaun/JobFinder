using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Search.Deduplication;
using Jobmatch.Search.Locations;

namespace Jobmatch.Tests.Pipeline.Deduplication;

/// <summary>
/// The probabilistic dedupe pass (R-117) runs before ranking: SameAd verdicts merge into the
/// most informative copy, Possible verdicts never merge, and the safety rules (same-portal
/// never SameAd, one absorbed listing per portal per group) bound the destructive step.
/// </summary>
public sealed class ProbabilisticDeduperTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København"], 55.6761, 12.5683, "DK", GeoPlaceType.City, 600_000),
        new GazetteerEntry("Aalborg", [], 57.0488, 9.9217, "DK", GeoPlaceType.City, 120_000),
    ]);

    private static readonly ProbabilisticMatcher Matcher = new(Gaz);

    private static Listing Make(
        string id, string title, string? company = "Acme", string? location = "Copenhagen",
        string portal = "portal-a", string description = "")
    {
        return new Listing(
            Id: id,
            Portal: portal,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Unknown,
            Description: description,
            Url: new Uri($"https://example.com/{id}"),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    private static ProbabilisticDedupeResult Merge(params Listing[] listings)
        => ProbabilisticDeduper.Merge(listings, Matcher);

    [Fact]
    public void SameAd_CrossPortal_Merges_Before_Ranking()
    {
        var result = Merge(
            Make("wd", "Senior Software Engineer", "SimCorp", "Copenhagen", portal: "workday"),
            Make("jx", "Senior Software Engineer", "SimCorp A/S", "København", portal: "jobindex"),
            Make("other", "Platform Engineer", "SimCorp", "Copenhagen", portal: "workday"));

        Assert.Equal(2, result.Deduped.Count);
        var group = Assert.Single(result.Merges);
        Assert.Single(group.MergedFromIds);
        var sighting = Assert.Single(result.SightingsByCanonical[group.CanonicalId]);
        Assert.InRange(sighting.Probability, 0.9, 1.0);
    }

    [Fact]
    public void The_Most_Informative_Copy_Survives()
    {
        // The located, full-text Workday req must outlive the null-location jobindex
        // re-listing — the ranker and the card can do more with it.
        var result = Merge(
            Make("jx", "Senior Software Engineer", "SimCorp", location: null, portal: "jobindex"),
            Make("wd", "Senior Software Engineer", "SimCorp", "Copenhagen", portal: "workday",
                description: new string('x', 500)));

        Assert.Equal("wd", Assert.Single(result.Deduped).Id);
        Assert.Equal("jx", Assert.Single(result.SightingsByCanonical["wd"]).Listing.Id);
    }

    [Fact]
    public void A_Canonical_Absorbs_At_Most_One_Listing_Per_Portal()
    {
        // Both Workday reqs exact-match the null-location jobindex ad; one ad appears once per
        // portal, so only one merges and the sibling survives with the pair recorded.
        var result = Merge(
            Make("jx", "Senior Software Engineer", "SimCorp", location: null, portal: "jobindex"),
            Make("w1", "Senior Software Engineer", "SimCorp", "Copenhagen", portal: "workday",
                description: new string('x', 500)),
            Make("w2", "Senior Software Engineer", "SimCorp", location: null, portal: "workday"));

        Assert.Equal(2, result.Deduped.Count);
        Assert.Contains(result.Deduped, l => l.Id == "w1");
        Assert.Contains(result.Deduped, l => l.Id == "w2");
        Assert.Equal("jx", Assert.Single(result.SightingsByCanonical["w1"]).Listing.Id);
        Assert.Contains(result.PossibleDuplicates, p => p.CandidateId == "w2" || p.KeptId == "w2");
    }

    [Fact]
    public void SamePortal_Pairs_Never_Merge()
    {
        var result = Merge(
            Make("a", "Senior Software Engineer", "Danske Bank", "Copenhagen"),
            Make("b", "Senior Software Engineer", "Danske Bank", "Copenhagen"));

        Assert.Equal(2, result.Deduped.Count);
        Assert.Empty(result.Merges);
        Assert.Single(result.PossibleDuplicates);
    }

    [Fact]
    public void Weak_Possible_Pairs_Are_Not_Recorded()
    {
        // Same title in two cities is p≈0.33 — a real distinct role, not worth an audit row.
        var result = Merge(
            Make("cph", "Grocery Associate", "Wolt", "Copenhagen"),
            Make("aal", "Grocery Associate", "Wolt", "Aalborg", portal: "portal-b"));

        Assert.Equal(2, result.Deduped.Count);
        Assert.Empty(result.PossibleDuplicates);
    }

    [Fact]
    public void Seniority_And_Stack_Conflicts_Never_Merge()
    {
        var result = Merge(
            Make("senior", "Senior Full-Stack Software Engineer (.Net/Angular)", "SimCorp", portal: "workday"),
            Make("lead", "Lead Full-Stack Software Engineer (.Net/Angular)", "SimCorp", portal: "jobindex"),
            Make("net", "Senior udvikler til afdeling i vækst (.Net)", "Sopra", portal: "a"),
            Make("java", "Senior udvikler til afdeling i vækst (Java)", "Sopra", portal: "b"));

        Assert.Equal(4, result.Deduped.Count);
        Assert.Empty(result.Merges);
    }

    [Fact]
    public void Missing_Company_Is_Always_Kept_And_Never_Compared()
    {
        var result = Merge(
            Make("a", "Senior Software Engineer", company: null),
            Make("b", "Senior Software Engineer", company: null, portal: "portal-b"));

        Assert.Equal(2, result.Deduped.Count);
        Assert.Empty(result.Merges);
        Assert.Empty(result.PossibleDuplicates);
    }

    [Fact]
    public void Possible_Pairs_List_CrossPortal_First_Then_By_Probability()
    {
        // A cross-portal pair (possible matcher miss) outranks a same-portal re-post, however
        // certain the re-post looks.
        var result = Merge(
            Make("a", "Senior Engineer", "Acme", "Copenhagen"),
            Make("b", "Senior Engineer", "Acme", "Copenhagen", portal: "portal-a", description: "x"),
            Make("c", "Senior/Lead Platform Engineer", "Globex", "Copenhagen", portal: "p1"),
            Make("d", "Senior Platform Engineer", "Globex", "Copenhagen", portal: "p2"));

        Assert.True(result.PossibleDuplicates.Count >= 2);
        Assert.False(result.PossibleDuplicates[0].SamePortal);
        Assert.True(result.PossibleDuplicates[^1].SamePortal);
    }

    [Fact]
    public void Company_Convention_Drift_Still_Merges_Via_The_Token_Block()
    {
        var result = Merge(
            Make("a", "Senior Engineer", "twoday", "Copenhagen", portal: "teamtailor"),
            Make("b", "Senior Engineer", "twoday Denmark", "København", portal: "jobindex"));

        Assert.Single(result.Deduped);
        Assert.Single(result.Merges);
    }

    [Fact]
    public void Result_Is_Independent_Of_Input_Order()
    {
        var listings = new[]
        {
            Make("jx", "Senior Software Engineer", "SimCorp", location: null, portal: "jobindex"),
            Make("wd", "Senior Software Engineer", "SimCorp", "Copenhagen", portal: "workday"),
            Make("other", "Platform Engineer", "SimCorp", "Copenhagen", portal: "workday"),
        };

        var forward = Merge(listings);
        var reversed = Merge([.. listings.Reverse()]);

        Assert.Equal(
            forward.Deduped.Select(l => l.Id).OrderBy(x => x, StringComparer.Ordinal),
            reversed.Deduped.Select(l => l.Id).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Deduped_Preserves_Input_Order_Of_Survivors()
    {
        var result = Merge(
            Make("z-last", "Platform Engineer", "SimCorp", portal: "workday"),
            Make("a-first", "Senior Data Engineer", "SimCorp", portal: "workday"));

        Assert.Equal(["z-last", "a-first"], result.Deduped.Select(l => l.Id));
    }
}
