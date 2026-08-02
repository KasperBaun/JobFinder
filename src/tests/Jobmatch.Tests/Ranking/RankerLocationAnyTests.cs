using System.Text.Json;
using Jobmatch.Models;
using Jobmatch.Ranking;

namespace Jobmatch.Tests.Ranking;

// remote_preference: any means "no remote-mode preference" — it never meant "any location on
// the globe". The location tier must still separate a listing in the user's city from a
// far-away office, and the reasoning must name the location instead of "not stated".
public sealed class RankerLocationAnyTests
{
    private static readonly RankingWeights Weights = new(
        PrimaryStack: 0.40,
        SecondaryStack: 0.15,
        Seniority: 0.15,
        LocationRemote: 0.15,
        Domain: 0.10,
        Freshness: 0.05);

    private static RankingConfig Cfg() => new(Weights, 0.0, 100, 14, 0.0);

    private static Skillset AnyPersona() => new(
        Name: "Kasper",
        Location: "Copenhagen",
        ExperienceYears: 7,
        TargetRoles: ["Software Engineer"],
        RemotePreference: RemotePreference.Any,
        Seniority: Seniority.Senior,
        PrimaryStack: ["C#", ".NET"],
        SecondaryStack: [],
        Domains: [],
        Disqualifiers: [],
        Languages: ["English"],
        EmploymentTypes: [])
    {
        Country = "Denmark",
        Region = "EU",
    };

    private static Listing MakeListing(string title, string? location, RemoteMode remote = RemoteMode.Unknown) =>
        new(
            Id: Guid.NewGuid().ToString("N"),
            Portal: "test",
            Title: title,
            Company: "TestCo",
            Location: location,
            RemoteMode: remote,
            Description: "C# and .NET.",
            Url: new Uri("https://example.com/" + Guid.NewGuid().ToString("N")),
            PostedAt: DateTimeOffset.UtcNow,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());

    [Fact]
    public void Any_CityListing_ScoresFullLocationSignal_AndNamesTheLocation()
    {
        var scored = Ranker.Score([MakeListing("Engineer", "Copenhagen")], AnyPersona(), Cfg());

        Assert.Equal(Weights.LocationRemote, scored[0].Breakdown.LocationRemote, 3);
        Assert.Equal(true, scored[0].Reasoning.LocationMatch);
        Assert.Contains(scored[0].Reasoning.NoteKeys!, n => n.Key == "location");
    }

    [Fact]
    public void Any_ForeignOffice_ScoresElseTier_NotFullCredit()
    {
        var scored = Ranker.Score([MakeListing("Engineer", "Bad Homburg")], AnyPersona(), Cfg());

        Assert.Equal(Weights.LocationRemote * LocationTierWeights.Default.Else, scored[0].Breakdown.LocationRemote, 3);
        Assert.Equal(false, scored[0].Reasoning.LocationMatch);
    }

    [Fact]
    public void Any_CityBeatsForeignOffice()
    {
        var scored = Ranker.Score(
            [MakeListing("Engineer", "Bad Homburg"), MakeListing("Engineer", "Copenhagen")],
            AnyPersona(), Cfg());

        var city = scored.Single(s => s.Listing.Location == "Copenhagen");
        var far = scored.Single(s => s.Listing.Location == "Bad Homburg");
        Assert.True(city.Score > far.Score, $"city {city.Score:0.000} must beat far {far.Score:0.000}");
    }

    [Fact]
    public void Any_MissingLocation_StaysNeutral()
    {
        var scored = Ranker.Score([MakeListing("Engineer", location: null)], AnyPersona(), Cfg());

        Assert.Equal(Weights.LocationRemote, scored[0].Breakdown.LocationRemote, 3);
        Assert.Null(scored[0].Reasoning.LocationMatch);
    }

    [Fact]
    public void Any_RemoteListing_InRegion_IsNotPenalised()
    {
        var scored = Ranker.Score(
            [MakeListing("Engineer", "Europe (remote)", RemoteMode.Remote)], AnyPersona(), Cfg());

        Assert.Equal(Weights.LocationRemote, scored[0].Breakdown.LocationRemote, 3);
    }

    [Fact]
    public void Any_RemoteListing_RestrictedToOtherRegion_ScoresItsTier()
    {
        var scored = Ranker.Score(
            [MakeListing("Engineer", "USA only", RemoteMode.Remote)], AnyPersona(), Cfg());

        Assert.Equal(Weights.LocationRemote * LocationTierWeights.Default.Else, scored[0].Breakdown.LocationRemote, 3);
    }
}
