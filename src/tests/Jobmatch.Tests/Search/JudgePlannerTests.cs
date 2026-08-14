using Jobmatch.Pipeline.Stages;
using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Geo;
using Jobmatch.Pipeline.Ranking;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Tests.Search;

/// <summary>
/// Blending reorders, so the shortlist taken afterwards is not the set that was judged before it.
/// The planner keeps handing out passes until every shortlist entry has been offered to the judge —
/// bounded by a verdict budget (llm.top_n + top_n) and a pass cap, so a run can never balloon.
/// </summary>
public sealed class JudgePlannerTests
{
    private static readonly Gazetteer Gaz = Gazetteer.FromEntries(
    [
        new GazetteerEntry("København", ["Copenhagen"], 55.6761, 12.5683, "DK", GeoPlaceType.City, 600_000),
        new GazetteerEntry("Aalborg", [], 57.0488, 9.9217, "DK", GeoPlaceType.City, 120_000),
    ]);

    private static readonly Skillset Home = new(
        Name: "Test User",
        Location: "København",
        ExperienceYears: 5,
        TargetRoles: ["Software Engineer"],
        RemotePreference: RemotePreference.Any,
        Seniority: Seniority.Mid,
        PrimaryStack: ["C#"],
        SecondaryStack: [],
        Domains: [],
        Disqualifiers: [],
        Languages: ["English"],
        EmploymentTypes: ["full-time"])
    {
        RadiusKm = 50,
        Latitude = 55.6761,
        Longitude = 12.5683,
    };

    private static RadiusFilter Radius() => RadiusFilter.Create(Home, Gaz)!;

    private static RankingConfig Ranking() => new(
        Weights: new RankingWeights(0.5, 0.1, 0.1, 0.2, 0.05, 0.05),
        DisqualifierPenalty: 0.0,
        TopN: 10,
        FreshnessHalfLifeDays: 14,
        MinScoreToInclude: 0.0);

    private static Match Scored(string id, double score, string location = "København")
    {
        var listing = new Listing(
            Id: id,
            Portal: "test",
            Title: id,
            Company: "Co",
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
            RemoteMatch: null,
            LocationMatch: null,
            DisqualifierHits: [],
            Notes: string.Empty);
        return new Match(listing, score, new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0), reasoning);
    }

    /// <summary>l1 = 0.90, l2 = 0.89, … so ordering is unambiguous.</summary>
    private static List<Match> Corpus(int count) => [.. Enumerable.Range(1, count).Select(i => Scored($"l{i}", 0.90 - i * 0.01))];

    /// <summary>What a judging pass does when the model dislikes what the keywords liked.</summary>
    private static List<Match> Demote(IEnumerable<Match> scored, params string[] ids)
        => [.. scored.Select(m => ids.Contains(m.Listing.Id) ? m with { Score = 0.01 } : m)];

    private static JudgePlanner Planner(int firstPassN, int topN, double minScore = 0.0)
        => new(Ranking(), minScore, topN, Radius(), firstPassN);

    private static IReadOnlyList<string> Ids(IReadOnlyList<Match> matches) => [.. matches.Select(m => m.Listing.Id)];

    [Fact]
    public void A_Stable_Shortlist_Costs_Nothing_Beyond_The_First_Pass()
    {
        var scored = Corpus(6);
        var planner = Planner(firstPassN: 3, topN: 3);

        var first = planner.Next(scored);
        var second = planner.Next(scored);

        Assert.Equal(["l1", "l2", "l3"], Ids(first));
        Assert.Empty(second);
        Assert.Equal(1, planner.Pass);
    }

    [Fact]
    public void Listings_Promoted_By_Blending_Get_A_Follow_Up_Pass()
    {
        var scored = Corpus(6);
        var planner = Planner(firstPassN: 3, topN: 3);

        planner.Next(scored);
        scored = Demote(scored, "l1", "l2");

        Assert.Equal(["l4", "l5"], Ids(planner.Next(scored)));
    }

    [Fact]
    public void A_Judged_Listing_Is_Never_Offered_Again()
    {
        var scored = Corpus(6);
        var planner = Planner(firstPassN: 1, topN: 3);

        Assert.Equal(["l1"], Ids(planner.Next(scored)));

        // l1 keeps its shortlist seat after judging; the follow-up must not pay for it twice.
        var second = planner.Next(scored);
        Assert.Equal(["l2", "l3"], Ids(second));
        Assert.Empty(planner.Next(scored));
    }

    [Fact]
    public void Follow_Ups_Respect_The_Drop_Set()
    {
        var scored = new List<Match>
        {
            Scored("far", 0.95, location: "Aalborg"),
            Scored("near-1", 0.80),
            Scored("near-2", 0.70),
            Scored("weak", 0.10),
        };
        var planner = Planner(firstPassN: 1, topN: 3, minScore: 0.5);

        Assert.Equal(["near-1"], Ids(planner.Next(scored)));

        // outside_radius stays dropped and below_min_score keeps `weak` off the shortlist, so the
        // follow-up buys verdicts for near-2 only.
        Assert.Equal(["near-2"], Ids(planner.Next(scored)));
        Assert.Empty(planner.Next(scored));
    }

    [Fact]
    public void Total_Verdicts_Are_Capped_At_FirstPass_Plus_Shortlist()
    {
        var scored = Corpus(20);
        var planner = Planner(firstPassN: 3, topN: 3);

        var spent = 0;
        for (var pass = 0; pass < 10; pass++)
        {
            var batch = planner.Next(scored);
            if (batch.Count == 0) break;
            spent += batch.Count;
            scored = Demote(scored, [.. Ids(batch)]);
        }

        Assert.Equal(6, spent);
        Assert.Equal(0, planner.Remaining);
        Assert.Empty(planner.Next(scored));
    }

    [Fact]
    public void Passes_Stop_At_The_Cap_Even_With_Budget_Left()
    {
        var scored = Corpus(12);
        var planner = Planner(firstPassN: 4, topN: 4);

        // One demotion per pass admits exactly one new entrant, so the budget outlasts the passes.
        var passes = 0;
        for (var i = 0; i < 10; i++)
        {
            var batch = planner.Next(scored);
            if (batch.Count == 0) break;
            passes++;
            scored = Demote(scored, batch[0].Listing.Id);
        }

        Assert.Equal(JudgePlanner.MaxPasses, passes);
        Assert.True(planner.Remaining > 0);
    }

    [Fact]
    public void FirstPassN_Zero_Judges_Everything_Eligible_And_Then_Stops()
    {
        var scored = Corpus(5);
        var planner = Planner(firstPassN: 0, topN: 3);

        Assert.Equal(5, planner.Next(scored).Count);
        Assert.Empty(planner.Next(Demote(scored, "l1", "l2")));
    }

    [Fact]
    public void An_Empty_Eligible_Set_Never_Starts_A_Pass()
    {
        var scored = new List<Match> { Scored("far", 0.95, location: "Aalborg") };
        var planner = Planner(firstPassN: 3, topN: 3);

        Assert.Empty(planner.Next(scored));
        Assert.Equal(0, planner.Pass);
    }
}
