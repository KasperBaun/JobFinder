using System.Text.Json;
using Jobmatch.Models;
using Jobmatch.Ranking;

namespace Jobmatch.Tests.Ranking;

public sealed class RankerTests
{
    private static readonly RankingWeights DefaultWeights = new(
        PrimaryStack: 0.40,
        SecondaryStack: 0.15,
        Seniority: 0.15,
        LocationRemote: 0.15,
        Domain: 0.10,
        Freshness: 0.05);

    private static RankingConfig RankingCfg(double minScore = 0.0, int topN = 100, double disqualifierPenalty = 0.0) =>
        new(DefaultWeights, disqualifierPenalty, topN, 14, minScore);

    private static Skillset MikkelPersona() => new(
        Name: "Mikkel",
        Location: "Copenhagen",
        ExperienceYears: 7,
        TargetRoles: ["Software Engineer", "Backend Developer"],
        RemotePreference: RemotePreference.Hybrid,
        Seniority: Seniority.Senior,
        PrimaryStack: ["C#", ".NET", "Azure", "SQL Server"],
        SecondaryStack: ["Docker", "Kubernetes"],
        Domains: ["internal tools", "B2B SaaS"],
        Disqualifiers: ["unpaid", "pure frontend"],
        Languages: ["English", "Danish"],
        EmploymentTypes: ["full-time", "consulting"]);

    private static Skillset LenaPersona() => new(
        Name: "Lena",
        Location: "Berlin",
        ExperienceYears: 4,
        TargetRoles: ["Full-stack Developer"],
        RemotePreference: RemotePreference.Remote,
        Seniority: Seniority.Mid,
        PrimaryStack: ["TypeScript", "React", "Rust"],
        SecondaryStack: ["Next.js", "PostgreSQL"],
        Domains: ["fintech", "developer tools"],
        Disqualifiers: ["relocation required"],
        Languages: ["English", "German"],
        EmploymentTypes: ["full-time"]);

    private static Listing MakeListing(string title, string description, string? location = null, RemoteMode remote = RemoteMode.Unknown, DateTimeOffset? postedAt = null) =>
        new(
            Id: Guid.NewGuid().ToString("N"),
            Portal: "test",
            Title: title,
            Company: "TestCo",
            Location: location,
            RemoteMode: remote,
            Description: description,
            Url: new Uri("https://example.com/" + Guid.NewGuid().ToString("N")),
            PostedAt: postedAt,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());

    [Fact]
    public void Rank_Strong_Primary_Match_Scores_High()
    {
        var listing = MakeListing(
            "Senior .NET Engineer",
            "We use C#, .NET, Azure and SQL Server to build internal tools.",
            location: "Copenhagen, Denmark",
            remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg());

        Assert.Single(matches);
        Assert.True(matches[0].Score > 0.75, $"expected score > 0.75, got {matches[0].Score:0.000}");
        Assert.Equal(4, matches[0].Reasoning.PrimaryStackHits.Count);
        Assert.True(matches[0].Reasoning.SeniorityMatch);
        Assert.True(matches[0].Reasoning.LocationMatch);
    }

    [Fact]
    public void Rank_Disqualifier_Hit_Zeroes_Score()
    {
        var listing = MakeListing(
            "Senior .NET Engineer",
            "We need C#, .NET, Azure — this is an unpaid internship.",
            remote: RemoteMode.Remote,
            postedAt: DateTimeOffset.UtcNow);

        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg(minScore: 0.0));
        Assert.Single(matches);
        Assert.Equal(0.0, matches[0].Score);
        Assert.Contains("unpaid", matches[0].Reasoning.DisqualifierHits);
    }

    [Fact]
    public void Rank_Stale_Listing_Freshness_Decays()
    {
        var fresh = MakeListing(
            "TypeScript/React Engineer",
            "Build the frontend in TypeScript, React, and Next.js.",
            remote: RemoteMode.Remote,
            postedAt: DateTimeOffset.UtcNow);
        var stale = fresh with
        {
            Id = Guid.NewGuid().ToString("N"),
            Url = new Uri("https://example.com/" + Guid.NewGuid().ToString("N")),
            PostedAt = DateTimeOffset.UtcNow.AddDays(-180),
        };

        var matches = Ranker.Rank([fresh, stale], LenaPersona(), RankingCfg());
        var freshMatch = matches.Single(m => m.Listing.Id == fresh.Id);
        var staleMatch = matches.Single(m => m.Listing.Id == stale.Id);

        Assert.True(freshMatch.Score > staleMatch.Score);
        Assert.True(freshMatch.Breakdown["freshness"] > staleMatch.Breakdown["freshness"]);
        Assert.True(staleMatch.Breakdown["freshness"] < 0.001);
    }

    [Fact]
    public void Rank_Partial_Primary_Match_Gets_Partial_Score()
    {
        var listing = MakeListing(
            "Backend Developer",
            "We use C# on the backend. Rest is JavaScript.",
            remote: RemoteMode.Remote,
            postedAt: DateTimeOffset.UtcNow);

        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg());
        Assert.Single(matches);
        Assert.True(matches[0].Score > 0.0);
        Assert.True(matches[0].Score < 0.75);
        Assert.Single(matches[0].Reasoning.PrimaryStackHits);
    }

    [Fact]
    public void Rank_Seniority_Any_Always_Scores_Full()
    {
        var skillset = MikkelPersona() with { Seniority = Seniority.Any };
        var listing = MakeListing("Software Engineer", "C# and .NET and Azure and SQL Server.", postedAt: DateTimeOffset.UtcNow, remote: RemoteMode.Remote);

        var matches = Ranker.Rank([listing], skillset, RankingCfg());
        Assert.True(matches[0].Breakdown["seniority"] >= DefaultWeights.Seniority - 0.0001,
            $"expected full seniority credit, got {matches[0].Breakdown["seniority"]:0.000}");
    }

    [Fact]
    public void Rank_Unknown_Seniority_Reports_Null()
    {
        var listing = MakeListing("Software Engineer", "C# and .NET and Azure and SQL Server.", postedAt: DateTimeOffset.UtcNow, remote: RemoteMode.Remote);
        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg());
        Assert.Null(matches[0].Reasoning.SeniorityMatch);
    }

    [Fact]
    public void Rank_Below_MinScore_Is_Dropped()
    {
        var listing = MakeListing("HR Specialist", "Payroll processing and benefits admin.", remote: RemoteMode.Remote);
        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg(minScore: 0.25));
        Assert.Empty(matches);
    }

    [Fact]
    public void Rank_Results_Are_Sorted_Descending_By_Score()
    {
        var weak = MakeListing("Some other job", "Content is not related.", postedAt: DateTimeOffset.UtcNow, remote: RemoteMode.Remote);
        var strong = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server internal tools.", location: "Copenhagen", remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow);

        var matches = Ranker.Rank([weak, strong], MikkelPersona(), RankingCfg(minScore: 0.0));
        Assert.Equal(2, matches.Count);
        Assert.Equal("Senior .NET Engineer", matches[0].Listing.Title);
        Assert.True(matches[0].Score >= matches[1].Score);
    }

    [Fact]
    public void Rank_TopN_Limits_Results()
    {
        var listings = Enumerable.Range(0, 10)
            .Select(i => MakeListing($"Senior .NET Engineer {i}", "C# .NET Azure SQL Server.", location: "Copenhagen", remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow))
            .ToList();

        var matches = Ranker.Rank(listings, MikkelPersona(), RankingCfg(topN: 3));
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Rank_All_Scores_In_Unit_Interval()
    {
        var listings = new[]
        {
            MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server internal tools.", location: "Copenhagen", remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow),
            MakeListing("Unrelated role", "We do pottery.", remote: RemoteMode.Onsite),
            MakeListing("Unpaid internship", "C# .NET Azure and SQL Server.", postedAt: DateTimeOffset.UtcNow, remote: RemoteMode.Remote),
        };
        var matches = Ranker.Rank(listings, MikkelPersona(), RankingCfg(minScore: 0.0));
        foreach (var m in matches)
        {
            Assert.InRange(m.Score, 0.0, 1.0);
        }
    }

    [Fact]
    public void Rank_Keyword_Matching_Is_CaseInsensitive_And_WholeWord()
    {
        var listing = MakeListing(
            "Senior Engineer",
            "Primary skills: c# and .NET and azure.",
            location: "Copenhagen",
            remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow);

        var matches = Ranker.Rank([listing], MikkelPersona(), RankingCfg());
        Assert.Equal(3, matches[0].Reasoning.PrimaryStackHits.Count);
    }
}
