using Jobmatch.Search.Judging;
using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Search.Locations;
using Jobmatch.Search.Ranking;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Tests.Pipeline.Stages;

/// <summary>
/// The LLM judge budget must be spent on listings that can still reach the shortlist. Judging the
/// raw keyword top-N left real shortlist entries unjudged (and silently keyword-scored) because
/// the budget went to listings the hard filters discarded moments later.
/// </summary>
public sealed class JudgeCandidatesTests
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

    private static RankingConfig Ranking(int? maxAgeDays = null, bool requirePrimary = false) => new(
        Weights: new RankingWeights(0.5, 0.1, 0.1, 0.2, 0.05, 0.05),
        DisqualifierPenalty: 0.0,
        TopN: 10,
        FreshnessHalfLifeDays: 14,
        MinScoreToInclude: 0.5,
        MaxAgeDays: maxAgeDays,
        RequirePrimaryStackHit: requirePrimary);

    private static Match Scored(
        string id,
        double score,
        string? location = null,
        DateTimeOffset? postedAt = null,
        RemoteMode remote = RemoteMode.Unknown,
        IReadOnlyList<string>? primaryHits = null,
        IReadOnlyList<string>? disqualifierHits = null)
    {
        var listing = new Listing(
            Id: id,
            Portal: "test",
            Title: id,
            Company: "Co",
            Location: location,
            RemoteMode: remote,
            Description: "desc",
            Url: new Uri($"https://example.com/{id}"),
            PostedAt: postedAt,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
        var reasoning = new MatchReasoning(
            PrimaryStackHits: primaryHits ?? ["C#"],
            SecondaryStackHits: [],
            DomainHits: [],
            SeniorityMatch: null,
            LocationMatch: null,
            RemoteMatch: null,
            DisqualifierHits: disqualifierHits ?? [],
            Notes: string.Empty);
        return new Match(listing, score, new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0), reasoning);
    }

    private static IReadOnlyList<string> Ids(IReadOnlyList<Match> matches) => [.. matches.Select(m => m.Listing.Id)];

    [Fact]
    public void Budget_Skips_Listings_Outside_The_Radius()
    {
        var scored = new List<Match>
        {
            Scored("far", 0.90, location: "Aalborg"),
            Scored("near", 0.40, location: "København"),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 1);

        Assert.Equal(["near"], Ids(candidates));
    }

    [Fact]
    public void Remote_Listing_Outside_The_Radius_Stays_Eligible()
    {
        var scored = new List<Match> { Scored("far-remote", 0.90, location: "Aalborg", remote: RemoteMode.Remote) };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 25);

        Assert.Equal(["far-remote"], Ids(candidates));
    }

    [Fact]
    public void Budget_Skips_Listings_Above_Max_Age()
    {
        var scored = new List<Match>
        {
            Scored("stale", 0.90, postedAt: DateTimeOffset.UtcNow.AddDays(-90)),
            Scored("fresh", 0.40, postedAt: DateTimeOffset.UtcNow.AddDays(-2)),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(maxAgeDays: 30), Radius(), topN: 25);

        Assert.Equal(["fresh"], Ids(candidates));
    }

    [Fact]
    public void Budget_Skips_Listings_Missing_The_Required_Primary_Stack()
    {
        var scored = new List<Match>
        {
            Scored("no-primary", 0.90, primaryHits: []),
            Scored("primary", 0.40),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(requirePrimary: true), Radius(), topN: 25);

        Assert.Equal(["primary"], Ids(candidates));
    }

    [Fact]
    public void Budget_Skips_Disqualified_Listings()
    {
        var scored = new List<Match>
        {
            Scored("dq", 0.90, disqualifierHits: ["clearance"]),
            Scored("clean", 0.40),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 25);

        Assert.Equal(["clean"], Ids(candidates));
    }

    // below_min_score is settled from the blended score, so a low keyword score must not cost a
    // listing its verdict — the judge is what can lift it over the threshold.
    [Fact]
    public void Low_Scoring_Listings_Stay_Eligible()
    {
        var scored = new List<Match> { Scored("weak", 0.01, location: "København") };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 25);

        Assert.Equal(["weak"], Ids(candidates));
    }

    [Fact]
    public void Candidates_Are_Ordered_By_Descending_Score_And_Capped_At_TopN()
    {
        var scored = new List<Match>
        {
            Scored("c", 0.30),
            Scored("a", 0.90),
            Scored("b", 0.60),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 2);

        Assert.Equal(["a", "b"], Ids(candidates));
    }

    [Fact]
    public void TopN_Zero_Judges_Every_Eligible_Listing()
    {
        var scored = new List<Match>
        {
            Scored("far", 0.90, location: "Aalborg"),
            Scored("near-1", 0.60, location: "København"),
            Scored("near-2", 0.30),
        };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), Radius(), topN: 0);

        Assert.Equal(["near-1", "near-2"], Ids(candidates));
    }

    [Fact]
    public void No_Radius_Filter_Leaves_Distance_Unjudged()
    {
        var scored = new List<Match> { Scored("far", 0.90, location: "Aalborg") };

        var candidates = JudgeCandidates.ForFirstPass(scored, Ranking(), radius: null, topN: 25);

        Assert.Equal(["far"], Ids(candidates));
    }
}
