using System.Text.Json;
using Jobmatch.Models;
using Jobmatch.Ranking;

namespace Jobmatch.Tests.Ranking;

public sealed class RankerPreferredCompanyTests
{
    private static readonly RankingWeights DefaultWeights = new(
        PrimaryStack: 0.40,
        SecondaryStack: 0.15,
        Seniority: 0.15,
        LocationRemote: 0.15,
        Domain: 0.10,
        Freshness: 0.05);

    private static RankingConfig RankingCfg() =>
        new(DefaultWeights, DisqualifierPenalty: 0.0, TopN: 100, FreshnessHalfLifeDays: 14, MinScoreToInclude: 0.0);

    private static Skillset Persona(params string[] preferredCompanies) => new(
        Name: "Mikkel",
        Location: "Copenhagen",
        ExperienceYears: 7,
        TargetRoles: ["Software Engineer"],
        RemotePreference: RemotePreference.Hybrid,
        Seniority: Seniority.Senior,
        PrimaryStack: ["C#", ".NET", "Azure", "SQL Server"],
        SecondaryStack: ["Docker"],
        Domains: ["internal tools"],
        Disqualifiers: ["unpaid"],
        Languages: ["English"],
        EmploymentTypes: ["full-time"])
    {
        Country = "Denmark",
        PreferredCompanies = preferredCompanies,
    };

    private static Listing MakeListing(string title, string description, string company, string? location = null) =>
        new(
            Id: Guid.NewGuid().ToString("N"),
            Portal: "test",
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: RemoteMode.Hybrid,
            Description: description,
            Url: new Uri("https://example.com/" + Guid.NewGuid().ToString("N")),
            PostedAt: DateTimeOffset.UtcNow.AddDays(-1),
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());

    [Fact]
    public void Preferred_Company_Match_Boosts_Score()
    {
        var listing = MakeListing(".NET Developer", "We use C# and .NET.", company: "LEGO");

        var baseline = Ranker.Score([listing], Persona(), RankingCfg())[0];
        var boosted = Ranker.Score([listing], Persona("LEGO"), RankingCfg())[0];

        Assert.True(boosted.Score > baseline.Score,
            $"expected boost: baseline={baseline.Score:0.000}, boosted={boosted.Score:0.000}");
        Assert.True(boosted.Breakdown.PreferredCompanyBonus > 0);
        Assert.Equal(0.0, baseline.Breakdown.PreferredCompanyBonus);
        Assert.Contains("LEGO", boosted.Reasoning.Notes);
    }

    [Fact]
    public void Preferred_Company_Boost_Caps_Score_At_One()
    {
        var listing = MakeListing(
            "Senior .NET Engineer",
            "C#, .NET, Azure, SQL Server, Docker — internal tools.",
            company: "SimCorp",
            location: "Copenhagen, Denmark");

        var matches = Ranker.Score([listing], Persona("SimCorp"), RankingCfg());

        Assert.True(matches[0].Score <= 1.0, $"score must clamp at 1.0, got {matches[0].Score:0.000}");
    }

    [Fact]
    public void Preferred_Company_In_Description_Only_Does_Not_Boost()
    {
        var listing = MakeListing(".NET Developer", "We partner with LEGO on internal tools.", company: "SomeAgency");

        var matches = Ranker.Score([listing], Persona("LEGO"), RankingCfg());

        Assert.Equal(0.0, matches[0].Breakdown.PreferredCompanyBonus);
    }

    [Fact]
    public void Preferred_Company_Matches_Token_Within_Company_Name()
    {
        var listing = MakeListing(".NET Developer", "C# and .NET.", company: "A.P. Moller - Maersk");

        var matches = Ranker.Score([listing], Persona("Maersk"), RankingCfg());

        Assert.True(matches[0].Breakdown.PreferredCompanyBonus > 0,
            "'Maersk' should match company 'A.P. Moller - Maersk'");
    }

    [Fact]
    public void Preferred_Company_Does_Not_Rescue_Disqualified_Listing()
    {
        var listing = MakeListing(".NET Developer — unpaid", "C# and .NET.", company: "LEGO");

        var matches = Ranker.Score([listing], Persona("LEGO"), RankingCfg());

        Assert.Equal(0.0, matches[0].Score);
    }

    [Fact]
    public void Preferred_Company_Boost_Is_Configurable()
    {
        var listing = MakeListing(".NET Developer", "We use C# and .NET.", company: "DFDS");

        var baseline = Ranker.Score([listing], Persona("DFDS"), RankingCfg())[0];
        var stronger = Ranker.Score([listing], Persona("DFDS"), RankingCfg() with { PreferredCompanyBoost = 2.0 })[0];

        Assert.True(stronger.Score > baseline.Score,
            $"boost 2.0 should beat default: default={baseline.Score:0.000}, 2.0={stronger.Score:0.000}");
    }
}
