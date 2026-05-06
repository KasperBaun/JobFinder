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
        EmploymentTypes: ["full-time", "consulting"])
    {
        Country = "Denmark",
        Region = "EU",
        Metro = ["Frederiksberg", "Hellerup", "Lyngby"],
    };

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
        Assert.True(freshMatch.Breakdown.Freshness > staleMatch.Breakdown.Freshness);
        Assert.True(staleMatch.Breakdown.Freshness < 0.001);
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
        Assert.True(matches[0].Breakdown.Seniority >= DefaultWeights.Seniority - 0.0001,
            $"expected full seniority credit, got {matches[0].Breakdown.Seniority:0.000}");
        // User asked Seniority.Any — the reasoning must say "fits", not "not stated".
        Assert.Equal(true, matches[0].Reasoning.SeniorityMatch);
    }

    [Fact]
    public void Rank_RemoteMatch_Boolean_Is_True_Only_For_Exact_Fit()
    {
        // Hybrid listing + Remote-preferring user scores 0.5 (partial), boolean must be false (not true).
        var listing = MakeListing("Engineer", "C# .NET Azure SQL Server", location: null, remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow);
        var skillset = MikkelPersona() with { RemotePreference = RemotePreference.Remote };
        var scored = Ranker.Score([listing], skillset, RankingCfg());
        Assert.Equal(false, scored[0].Reasoning.RemoteMatch);
    }

    [Fact]
    public void Rank_RemoteMatch_Boolean_Is_True_For_Exact_Fit()
    {
        var listing = MakeListing("Engineer", "C# .NET Azure SQL Server", remote: RemoteMode.Remote, postedAt: DateTimeOffset.UtcNow);
        var skillset = MikkelPersona() with { RemotePreference = RemotePreference.Remote };
        var scored = Ranker.Score([listing], skillset, RankingCfg());
        Assert.Equal(true, scored[0].Reasoning.RemoteMatch);
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
    public void Score_Returns_Everything_Unfiltered_Even_Below_MinScore()
    {
        var weak = MakeListing("Unrelated", "pottery", remote: RemoteMode.Remote);
        var strong = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server", location: "Copenhagen", remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([weak, strong], MikkelPersona(), RankingCfg(minScore: 0.5));
        Assert.Equal(2, scored.Count);
    }

    [Fact]
    public void Filter_Respects_MinScore_And_TopN()
    {
        var weak = MakeListing("Unrelated", "pottery", remote: RemoteMode.Remote);
        var strong = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server", location: "Copenhagen", remote: RemoteMode.Hybrid, postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([weak, strong], MikkelPersona(), RankingCfg());
        var filtered = Ranker.Filter(scored, RankingCfg(minScore: 0.5, topN: 1));
        Assert.Single(filtered);
        Assert.Equal("Senior .NET Engineer", filtered[0].Listing.Title);
    }

    [Fact]
    public void Score_Stale_Listing_Reasoning_Mentions_Age()
    {
        var stale = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-90));
        var scored = Ranker.Score([stale], MikkelPersona(), RankingCfg());
        Assert.Contains("days ago", scored[0].Reasoning.Notes);
    }

    [Fact]
    public void Score_Disqualified_Listing_Retains_DisqualifierHits()
    {
        var listing = MakeListing("Senior .NET Engineer",
            "C# .NET Azure SQL Server unpaid internship.",
            remote: RemoteMode.Remote);
        var scored = Ranker.Score([listing], MikkelPersona(), RankingCfg());
        Assert.Single(scored);
        Assert.Contains("unpaid", scored[0].Reasoning.DisqualifierHits);
    }

    [Fact]
    public void Rank_Remote_Listing_Restricted_To_Foreign_Region_Is_Penalized()
    {
        // Copenhagen-based user, prefers remote. An EU/worldwide-open remote listing
        // should score higher on location_remote than a remote listing restricted to
        // a non-EU region (e.g. "USA only") that the user can't actually take.
        var skillset = MikkelPersona() with { RemotePreference = RemotePreference.Remote };

        var euOpen = MakeListing("Eng A", "C# .NET Azure SQL Server",
            location: "Europe, European timezones",
            remote: RemoteMode.Remote, postedAt: DateTimeOffset.UtcNow);
        var usOnly = MakeListing("Eng B", "C# .NET Azure SQL Server",
            location: "USA only",
            remote: RemoteMode.Remote, postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([euOpen, usOnly], skillset, RankingCfg());
        var euLR = scored.Single(s => s.Listing.Title == "Eng A").Breakdown.LocationRemote;
        var usLR = scored.Single(s => s.Listing.Title == "Eng B").Breakdown.LocationRemote;

        Assert.True(euLR > usLR,
            $"EU-open should beat US-only on location_remote: euLR={euLR:0.000}, usLR={usLR:0.000}");
        // US-only must drop substantially below the full weight (it currently sits at full).
        Assert.True(usLR < 0.5 * DefaultWeights.LocationRemote,
            $"US-only should be < half of full location_remote weight, got {usLR:0.000}");
    }

    [Fact]
    public void Score_Location_Tiers_City_Metro_Country_Region_Else_In_Order()
    {
        // Mikkel is in Copenhagen; Frederiksberg is in Mikkel.Metro; Aarhus is in Denmark (country);
        // Berlin is in EU (region); USA-only is restrictive (else).
        var pref = MikkelPersona() with { RemotePreference = RemotePreference.Onsite };
        var w = DefaultWeights.LocationRemote;

        var city = MakeListing("A", "C# .NET Azure SQL Server",
            location: "Copenhagen, Denmark", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);
        var metro = MakeListing("B", "C# .NET Azure SQL Server",
            location: "Frederiksberg, Denmark", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);
        var country = MakeListing("C", "C# .NET Azure SQL Server",
            location: "Aarhus, Denmark", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);
        var region = MakeListing("D", "C# .NET Azure SQL Server",
            location: "Berlin, European timezones", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);
        var elsewhere = MakeListing("E", "C# .NET Azure SQL Server",
            location: "USA only", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([city, metro, country, region, elsewhere], pref, RankingCfg());
        double LR(string id) => scored.Single(s => s.Listing.Id == id).Breakdown.LocationRemote;

        // Strictly descending tiers.
        Assert.True(LR(city.Id) > LR(metro.Id), $"city ({LR(city.Id):0.000}) should beat metro ({LR(metro.Id):0.000})");
        Assert.True(LR(metro.Id) > LR(country.Id), $"metro ({LR(metro.Id):0.000}) should beat country ({LR(country.Id):0.000})");
        Assert.True(LR(country.Id) > LR(region.Id), $"country ({LR(country.Id):0.000}) should beat region ({LR(region.Id):0.000})");
        Assert.True(LR(region.Id) > LR(elsewhere.Id), $"region ({LR(region.Id):0.000}) should beat else ({LR(elsewhere.Id):0.000})");

        // City scores at full weight; else scores at 0.1*weight.
        Assert.Equal(w * 1.0, LR(city.Id), 3);
        Assert.Equal(w * 0.85, LR(metro.Id), 3);
        Assert.Equal(w * 0.6, LR(country.Id), 3);
    }

    [Fact]
    public void Score_Unknown_RemoteMode_Falls_Back_To_Location_Tier()
    {
        // When the adapter couldn't infer remote/hybrid/onsite, location alone should
        // drive the score — otherwise a perfectly local listing would score 0.
        var listing = MakeListing("Software Developer Intern", "TypeScript",
            location: "Hellerup, Denmark",
            remote: RemoteMode.Unknown,
            postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([listing], MikkelPersona(), RankingCfg());

        // Hellerup is in Mikkel's metro list — expect ~0.85 * weight.
        Assert.Equal(DefaultWeights.LocationRemote * 0.85, scored[0].Breakdown.LocationRemote, 3);
    }

    [Fact]
    public void Score_Worldwide_Listing_Is_Top_Tier_For_Any_User()
    {
        var pref = MikkelPersona() with { RemotePreference = RemotePreference.Remote };
        var worldwide = MakeListing("W", "C# .NET Azure SQL Server",
            location: "Worldwide / fully remote",
            remote: RemoteMode.Remote, postedAt: DateTimeOffset.UtcNow);

        var scored = Ranker.Score([worldwide], pref, RankingCfg());
        Assert.Equal(DefaultWeights.LocationRemote, scored[0].Breakdown.LocationRemote, 3);
    }

    [Fact]
    public void Score_Custom_Tier_Weights_Are_Honored()
    {
        var pref = MikkelPersona() with { RemotePreference = RemotePreference.Onsite };
        var country = MakeListing("C", "C# .NET Azure SQL Server",
            location: "Aarhus, Denmark", remote: RemoteMode.Onsite, postedAt: DateTimeOffset.UtcNow);

        var rankingDefault = RankingCfg();
        var rankingCustom = RankingCfg() with { LocationTierWeights = new LocationTierWeights(1.0, 0.9, 0.2, 0.1, 0.0) };

        var def = Ranker.Score([country], pref, rankingDefault)[0].Breakdown.LocationRemote;
        var cus = Ranker.Score([country], pref, rankingCustom)[0].Breakdown.LocationRemote;

        // Custom country tier (0.2) should produce a lower contribution than the default (0.6).
        Assert.True(cus < def, $"custom (0.2) should yield smaller contribution than default (0.6); cus={cus:0.000} def={def:0.000}");
    }

    [Fact]
    public void Filter_Drops_Listings_With_No_Primary_Stack_Hit_When_Required()
    {
        var marketing = MakeListing("Brand & Growth Marketer",
            "Drive marketing campaigns for our B2B SaaS product. Manage budget.",
            location: "Copenhagen", remote: RemoteMode.Onsite,
            postedAt: DateTimeOffset.UtcNow);
        var engineering = MakeListing("Senior .NET Engineer",
            "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow);
        var ranking = RankingCfg() with { RequirePrimaryStackHit = true };

        var scored = Ranker.Score([marketing, engineering], MikkelPersona(), ranking);
        var filtered = Ranker.Filter(scored, ranking);

        Assert.Single(filtered);
        Assert.Equal(engineering.Id, filtered[0].Listing.Id);
    }

    [Fact]
    public void Filter_RequirePrimaryStackHit_Off_Keeps_Listings_Without_Primary_Hits()
    {
        var marketing = MakeListing("Brand & Growth Marketer",
            "Drive marketing campaigns for our B2B SaaS product.",
            location: "Copenhagen", remote: RemoteMode.Onsite,
            postedAt: DateTimeOffset.UtcNow);
        var ranking = RankingCfg(); // RequirePrimaryStackHit defaults to false

        var scored = Ranker.Score([marketing], MikkelPersona(), ranking);
        var filtered = Ranker.Filter(scored, ranking);

        // Marketing listing has no primary hits but is still scored on location/seniority/domain.
        // It must be retained when the gate is off.
        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_Drops_Listings_Older_Than_MaxAgeDays()
    {
        var fresh = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-5));
        var stale = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-90));
        var ranking = RankingCfg() with { MaxAgeDays = 14 };

        var scored = Ranker.Score([fresh, stale], MikkelPersona(), ranking);
        var filtered = Ranker.Filter(scored, ranking);

        Assert.Single(filtered);
        Assert.Equal(fresh.Id, filtered[0].Listing.Id);
    }

    [Fact]
    public void Filter_Listing_With_Null_PostedAt_Is_Kept_When_MaxAgeDays_Set()
    {
        var noDate = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: null);
        var ranking = RankingCfg() with { MaxAgeDays = 14 };

        var scored = Ranker.Score([noDate], MikkelPersona(), ranking);
        var filtered = Ranker.Filter(scored, ranking);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_MaxAgeDays_Null_Disables_Cutoff()
    {
        var ancient = MakeListing("Senior .NET Engineer", "C# .NET Azure SQL Server",
            location: "Copenhagen", remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-365));
        var ranking = RankingCfg(); // MaxAgeDays defaults to null

        var scored = Ranker.Score([ancient], MikkelPersona(), ranking);
        var filtered = Ranker.Filter(scored, ranking);

        Assert.Single(filtered);
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

    [Fact]
    public void ScoreBreakdown_Components_Sum_To_PreClamp_Score()
    {
        var listing = MakeListing(
            "Senior .NET Engineer",
            "We use C#, .NET, Azure and SQL Server. Internal tools. Docker.",
            location: "Copenhagen, Denmark",
            remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow.AddDays(-3));

        var scored = Ranker.Score([listing], MikkelPersona(), RankingCfg());
        var b = scored[0].Breakdown;

        var sum = b.PrimaryStack + b.SecondaryStack + b.Seniority
            + b.LocationRemote + b.Domain + b.Freshness + b.DisqualifierPenalty;

        // No disqualifier hit on this listing — penalty must be exactly 0.
        Assert.Equal(0.0, b.DisqualifierPenalty);
        Assert.Equal(scored[0].Score, sum, precision: 4);
    }

    [Fact]
    public void ScoreBreakdown_DisqualifierPenalty_Is_Negative_When_Triggered()
    {
        var listing = MakeListing(
            "Senior .NET Engineer",
            "We use C#, .NET, Azure. This is unpaid.",
            location: "Copenhagen, Denmark",
            remote: RemoteMode.Hybrid,
            postedAt: DateTimeOffset.UtcNow);

        // Penalty = 0 zeroes the score; the breakdown's disqualifier delta must
        // equal the negation of the pre-penalty positive components.
        var ranking = RankingCfg(disqualifierPenalty: 0.0);
        var scored = Ranker.Score([listing], MikkelPersona(), ranking);
        var b = scored[0].Breakdown;

        Assert.Single(scored[0].Reasoning.DisqualifierHits);
        Assert.True(b.DisqualifierPenalty < 0, $"penalty must be < 0 when triggered, got {b.DisqualifierPenalty:0.000}");
        var positives = b.PrimaryStack + b.SecondaryStack + b.Seniority + b.LocationRemote + b.Domain + b.Freshness;
        Assert.Equal(-positives, b.DisqualifierPenalty, precision: 4);
        Assert.Equal(0.0, scored[0].Score);
    }

    [Fact]
    public void ScoreBreakdown_EnumerateComponents_Returns_All_Seven_In_Stable_Order()
    {
        var b = new ScoreBreakdown(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, -0.7);
        var labels = b.EnumerateComponents().Select(c => c.Label).ToList();
        Assert.Equal(
            ["primary_stack", "secondary_stack", "seniority", "location_remote", "domain", "freshness", "disqualifier_penalty"],
            labels);
    }
}
