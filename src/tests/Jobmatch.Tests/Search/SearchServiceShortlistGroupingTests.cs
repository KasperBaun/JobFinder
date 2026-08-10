using System.Text.Json;
using Jobmatch.Deduplication;
using Jobmatch.Geo;
using Jobmatch.Models;
using Jobmatch.Search;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Tests.Search;

/// <summary>
/// Shortlist-time grouping (R-117): a candidate the probabilistic matcher deems the same ad as a
/// seated slot folds into that slot as a sighting — freeing the slot for the next distinct role —
/// while Possible pairs are only recorded and never cost anyone a slot.
/// </summary>
public sealed class SearchServiceShortlistGroupingTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("Copenhagen", ["København"], 55.6761, 12.5683, "DK", GeoPlaceType.City, 600_000),
        new GazetteerEntry("Aalborg", [], 57.0488, 9.9217, "DK", GeoPlaceType.City, 120_000),
    ]);

    private static readonly ProbabilisticMatcher Matcher = new(Gaz);

    private static RankingConfig Ranking(int topN = 10) => new(
        Weights: new RankingWeights(0.5, 0.1, 0.1, 0.2, 0.05, 0.05),
        DisqualifierPenalty: 0.0,
        TopN: topN,
        FreshnessHalfLifeDays: 14,
        MinScoreToInclude: 0.0,
        MaxAgeDays: null,
        RequirePrimaryStackHit: false);

    private static Match Scored(
        string id, double score, string title, string? company = "Acme", string? location = "Copenhagen")
    {
        var listing = new Listing(
            Id: id,
            Portal: $"portal-{id}",
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Unknown,
            Description: "desc",
            Url: new Uri($"https://example.com/{id}"),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
        var reasoning = new MatchReasoning(
            PrimaryStackHits: ["C#"],
            SecondaryStackHits: [],
            DomainHits: [],
            SeniorityMatch: null,
            LocationMatch: null,
            RemoteMatch: null,
            DisqualifierHits: [],
            Notes: string.Empty);
        return new Match(listing, score, new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0), reasoning);
    }

    private static SearchService.ShortlistSelection Build(IReadOnlyList<Match> scored, int topN)
        => SearchService.BuildShortlist(scored, Ranking(topN), minScore: 0.0, topN, radius: null, Matcher);

    [Fact]
    public void SameAd_Candidate_Folds_Into_The_Slot_And_Frees_It_For_The_Next_Role()
    {
        var selection = Build(
        [
            Scored("oracle", 0.9, "Senior Software Engineer C#/.net", location: "Copenhagen V, Denmark"),
            Scored("jobindex", 0.85, "Senior Software Engineer C#/.net", location: "København"),
            Scored("other", 0.6, "Platform Engineer"),
        ], topN: 2);

        Assert.Equal(["oracle", "other"], selection.Shortlist.Select(m => m.Listing.Id));

        var sightings = Assert.Single(selection.SightingsByPrimary);
        Assert.Equal("oracle", sightings.Key);
        Assert.Equal("jobindex", Assert.Single(sightings.Value).Match.Listing.Id);

        var drop = Assert.Single(selection.Dropped, d => d.Id == "jobindex");
        Assert.Equal("duplicate_of_shortlisted", drop.Reason);
        Assert.Empty(selection.PossibleDuplicates);
    }

    [Fact]
    public void SameAd_Candidate_Beyond_The_Cut_Is_Absorbed_Not_Ranked()
    {
        var selection = Build(
        [
            Scored("a", 0.9, "Senior Software Engineer"),
            Scored("b", 0.8, "Platform Engineer"),
            Scored("c", 0.7, "Senior Software Engineer", location: null),
        ], topN: 1);

        Assert.Equal(["a"], selection.Shortlist.Select(m => m.Listing.Id));
        Assert.Equal("c", Assert.Single(selection.SightingsByPrimary["a"]).Match.Listing.Id);
        Assert.Equal("duplicate_of_shortlisted", Assert.Single(selection.Dropped, d => d.Id == "c").Reason);
        Assert.Equal("beyond_top_n", Assert.Single(selection.Dropped, d => d.Id == "b").Reason);
    }

    [Fact]
    public void A_Slot_Absorbs_At_Most_One_Sighting_Per_Portal()
    {
        // Seen live (run 20260810-080352): a null-location jobindex re-listing wildcards every
        // city, so it claimed BOTH Workday reqs of the same title. One ad appears once per
        // portal — the second claimant is the portal's other req and keeps its own candidacy.
        var jobindex = Scored("jx", 0.9, "Senior Software Engineer", company: "SimCorp", location: null);
        var workday1 = MakePortal("w1", 0.85, "Senior Software Engineer", "workday-simcorp", "Copenhagen");
        var workday2 = MakePortal("w2", 0.84, "Senior Software Engineer", "workday-simcorp", "Bad Homburg");

        var selection = Build([jobindex, workday1, workday2], topN: 5);

        Assert.Equal(["jx", "w2"], selection.Shortlist.Select(m => m.Listing.Id));
        Assert.Equal("w1", Assert.Single(selection.SightingsByPrimary["jx"]).Match.Listing.Id);
        var pair = Assert.Single(selection.PossibleDuplicates);
        Assert.Equal(("jx", "w2"), (pair.KeptId, pair.CandidateId));
    }

    private static Match MakePortal(string id, double score, string title, string portal, string? location)
    {
        var m = Scored(id, score, title, company: "SimCorp", location: location);
        return m with { Listing = m.Listing with { Portal = portal } };
    }

    [Fact]
    public void Possible_Pair_Is_Recorded_And_Both_Keep_Their_Slots()
    {
        var selection = Build(
        [
            Scored("cph", 0.9, "Grocery Associate", company: "Wolt"),
            Scored("aal", 0.8, "Grocery Associate", company: "Wolt", location: "Aalborg"),
        ], topN: 5);

        Assert.Equal(2, selection.Shortlist.Count);
        Assert.Empty(selection.SightingsByPrimary);
        var pair = Assert.Single(selection.PossibleDuplicates);
        Assert.Equal(("cph", "aal"), (pair.KeptId, pair.CandidateId));
        Assert.InRange(pair.Probability, 0.01, 0.89);
    }

    [Fact]
    public void Distinct_Seniority_Variants_Keep_Their_Slots_With_No_Possible_Pair()
    {
        var selection = Build(
        [
            Scored("senior", 0.9, "Senior Full-Stack Software Engineer (.Net/Angular)", company: "SimCorp"),
            Scored("lead", 0.8, "Lead Full-Stack Software Engineer (.Net/Angular)", company: "SimCorp"),
        ], topN: 5);

        Assert.Equal(2, selection.Shortlist.Count);
        Assert.Empty(selection.SightingsByPrimary);
        Assert.Empty(selection.PossibleDuplicates);
    }

    [Fact]
    public void Without_A_Matcher_Selection_Is_Pure_TopN()
    {
        var scored = new[]
        {
            Scored("a", 0.9, "Senior Software Engineer"),
            Scored("b", 0.8, "Senior Software Engineer"),
        };
        var selection = SearchService.BuildShortlist(scored, Ranking(1), 0.0, 1, radius: null);

        Assert.Equal(["a"], selection.Shortlist.Select(m => m.Listing.Id));
        Assert.Empty(selection.SightingsByPrimary);
        Assert.Empty(selection.PossibleDuplicates);
        Assert.Equal("beyond_top_n", Assert.Single(selection.Dropped).Reason);
    }
}
