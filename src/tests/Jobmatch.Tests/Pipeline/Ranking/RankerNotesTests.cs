using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Ranking;

namespace Jobmatch.Tests.Pipeline.Ranking;

/// <summary>
/// The rationale is emitted as keys + args so the GUI can render it in the user's language.
/// These pin two things: the key chosen for each branch (the keys are persisted in run history,
/// so they are a contract), and that the English rendering still reads exactly as the prose it
/// replaced — which is what top-jobs.md and pre-existing runs rely on.
/// </summary>
public sealed class RankerNotesTests
{
    private static Listing Listing(string? location = null, RemoteMode remote = RemoteMode.Unknown) => new(
        Id: "1", Portal: "test", Title: "Senior .NET Engineer", Company: "TestCo",
        Location: location, RemoteMode: remote, Description: "desc",
        Url: new Uri("https://example.com/1"), PostedAt: null, FetchedAt: DateTimeOffset.UtcNow,
        Raw: System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone());

    private static IReadOnlyList<ReasoningNote> Build(
        IReadOnlyList<string>? primary = null,
        IReadOnlyList<string>? secondary = null,
        IReadOnlyList<string>? domains = null,
        bool? seniorityMatch = true,
        bool seniorityIsAdjacent = false,
        bool? locationMatch = null,
        bool? remoteMatch = null,
        IReadOnlyList<string>? disqualifiers = null,
        bool nonEngineeringTitle = false,
        Listing? listing = null,
        double? ageDays = null,
        double halfLife = 14) =>
        Ranker.BuildNotes(
            primary ?? [], secondary ?? [], domains ?? [],
            seniorityMatch, seniorityIsAdjacent, locationMatch, remoteMatch,
            disqualifiers ?? [], nonEngineeringTitle, listing ?? Listing(), ageDays, halfLife);

    private static string[] Keys(IReadOnlyList<ReasoningNote> notes) => notes.Select(n => n.Key).ToArray();

    [Fact]
    public void Disqualifier_Short_Circuits_To_A_Single_Note()
    {
        var notes = Build(primary: ["C#"], disqualifiers: ["unpaid", "agency"]);

        Assert.Equal(["disqualified"], Keys(notes));
        Assert.Equal("Removed because of: unpaid, agency.", Ranker.RenderEnglish(notes));
    }

    [Theory]
    [InlineData(true, false, "seniorityMatches", "Experience level matches.")]
    [InlineData(true, true, "seniorityClose", "Experience level close fit.")]
    [InlineData(false, false, "seniorityMismatch", "Experience level doesn’t match.")]
    [InlineData(null, false, "seniorityUnknown", "Experience level not stated.")]
    public void Every_Seniority_Branch_Has_A_Key(bool? match, bool adjacent, string key, string english)
    {
        var notes = Build(seniorityMatch: match, seniorityIsAdjacent: adjacent);

        Assert.Contains(key, Keys(notes));
        Assert.Contains(english, Ranker.RenderEnglish(notes));
    }

    [Theory]
    [InlineData(true, null, "location")]
    [InlineData(null, true, "remoteOk")]
    [InlineData(false, false, "neitherLocationNorRemote")]
    [InlineData(null, null, "locationRemoteUnknown")]
    [InlineData(false, null, "locationMismatchRemoteUnknown")]
    [InlineData(null, false, "locationUnknownRemoteMismatch")]
    public void Every_Location_Branch_Has_A_Key(bool? location, bool? remote, string key)
    {
        var notes = Build(locationMatch: location, remoteMatch: remote,
            listing: Listing("Copenhagen", RemoteMode.Hybrid));

        Assert.Contains(key, Keys(notes));
    }

    [Fact]
    public void Notes_Carry_The_Values_The_Wording_Interpolates()
    {
        var notes = Build(
            primary: ["C#", ".NET"], secondary: ["Docker"], domains: ["fintech"],
            locationMatch: true, listing: Listing("Copenhagen"), ageDays: 41, halfLife: 14);

        var primary = notes.Single(n => n.Key == "primaryHits");
        Assert.Equal(new[] { "C#", ".NET" }, Assert.IsAssignableFrom<IEnumerable<string>>(primary.Args!["skills"]));

        var location = notes.Single(n => n.Key == "location");
        Assert.Equal("Copenhagen", location.Args!["location"]);

        var age = notes.Single(n => n.Key == "agePenalty");
        Assert.Equal(41, age.Args!["days"]);
    }

    [Fact]
    public void Age_Penalty_Only_Applies_Past_Twice_The_Half_Life()
    {
        Assert.DoesNotContain("agePenalty", Keys(Build(ageDays: 20, halfLife: 14)));
        Assert.Contains("agePenalty", Keys(Build(ageDays: 30, halfLife: 14)));
    }

    [Fact]
    public void English_Rendering_Reads_As_The_Prose_It_Replaced()
    {
        var notes = Build(
            primary: ["C#", ".NET"], secondary: ["Docker"], domains: ["fintech"],
            seniorityMatch: true, nonEngineeringTitle: true,
            remoteMatch: true, listing: Listing("Copenhagen", RemoteMode.Hybrid),
            ageDays: 41, halfLife: 14);

        Assert.Equal(
            "Must-have skill match: C#, .NET. Also matches: Docker. Industry: fintech. "
            + "Experience level matches. Title doesn’t look like a developer role — rating reduced. "
            + "Remote work OK (hybrid). Posted 41 days ago — rating reduced for age.",
            Ranker.RenderEnglish(notes));
    }

    [Fact]
    public void No_Primary_Hits_Says_So()
    {
        var notes = Build(primary: []);

        Assert.Contains("noPrimaryHits", Keys(notes));
        Assert.Contains("None of your must-have skills mentioned.", Ranker.RenderEnglish(notes));
    }

    [Fact]
    public void English_Rendering_Matches_The_Shared_Fixture()
    {
        // The same fixture is asserted by the frontend's Vitest suite. The wording exists twice by
        // necessity — C# renders top-jobs.md and the persisted prose, TypeScript renders the UI —
        // and this is what keeps the two identical. They had already drifted on apostrophes.
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "reasoning-en.json"));
        using var doc = JsonDocument.Parse(json);

        var checked_ = 0;
        foreach (var entry in doc.RootElement.GetProperty("notes").EnumerateArray())
        {
            var key = entry.GetProperty("key").GetString()!;
            var expected = entry.GetProperty("english").GetString()!;
            var args = new Dictionary<string, object>();
            foreach (var arg in entry.GetProperty("args").EnumerateObject())
            {
                args[arg.Name] = arg.Value.ValueKind switch
                {
                    JsonValueKind.Array => arg.Value.EnumerateArray().Select(v => v.GetString()!).ToArray(),
                    JsonValueKind.Number => arg.Value.GetInt32(),
                    _ => arg.Value.GetString()!,
                };
            }

            Assert.Equal(expected, Ranker.RenderEnglish([new ReasoningNote(key, args)]));
            checked_++;
        }

        // Every key BuildNotes can emit must appear, or the fixture stops being a guard.
        Assert.Equal(17, checked_);
    }
}
