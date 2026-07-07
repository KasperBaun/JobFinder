using System.Text.Json;
using Jobmatch.Json;
using Jobmatch.Llm;
using Jobmatch.Models;
using Jobmatch.Search;
using Jobmatch.Services;

namespace Jobmatch.Tests.Llm;

public sealed class MarkedExamplesLoaderTests : IDisposable
{
    private readonly string _historyDir;

    public MarkedExamplesLoaderTests()
    {
        _historyDir = Path.Combine(Path.GetTempPath(), "jobmatch-marked-examples-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_historyDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_historyDir)) Directory.Delete(_historyDir, recursive: true); } catch { }
    }

    private void WriteHistory(string runId, IReadOnlyList<ScoredEntry>? scored = null, IReadOnlyList<ListingMatch>? shortlist = null)
    {
        var detail = new RunDetail(
            RunId: runId,
            StartedAt: DateTimeOffset.UtcNow,
            Providers: [],
            FetchedCount: 0,
            DedupedCount: 0,
            RankedCount: 0,
            ShortlistCount: shortlist?.Count ?? 0,
            TopScore: 0.0,
            GoodMarks: 0,
            Shortlist: shortlist ?? [],
            Marks: new Dictionary<string, string>(),
            Scored: scored);
        File.WriteAllText(
            Path.Combine(_historyDir, $"{runId}.json"),
            JsonSerializer.Serialize(detail, JobmatchJsonOptions.Indented));
    }

    private static ScoredEntry Scored(string id, string title, string company) =>
        new(id, title, company, "Copenhagen", "https://x/" + id, null, "portal", 0.8,
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0), ["C#"], []);

    private static ListingMatch Match(string id, string title, string company) =>
        new(id, "portal", title, company, "Aarhus", "onsite", "https://x/" + id, null, 0.7, "", [".NET"], []);

    private static Dictionary<string, IReadOnlyDictionary<string, ListingMark>> Marks(
        params (string RunId, string ListingId, ListingMark Mark)[] entries)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, ListingMark>>();
        foreach (var group in entries.GroupBy(e => e.RunId))
        {
            result[group.Key] = group.ToDictionary(e => e.ListingId, e => e.Mark);
        }
        return result;
    }

    [Fact]
    public void Load_MarkedScoredListing_BecomesExampleWithReason()
    {
        WriteHistory("20260701-100000-aaaaaa", scored: [Scored("l1", "AI Engineer - Student", "Uni Co")]);
        var marks = Marks(("20260701-100000-aaaaaa", "l1", new ListingMark("bad", "I'm not a student")));

        var example = Assert.Single(MarkedExamplesLoader.Load(_historyDir, marks));
        Assert.Equal("disliked", example.Polarity);
        Assert.Equal("AI Engineer - Student", example.Title);
        Assert.Equal("Uni Co", example.Company);
        Assert.Equal("I'm not a student", example.Note);
        Assert.Equal(["C#"], example.PrimaryStack);
    }

    [Fact]
    public void Load_GoodMark_BecomesLikedExample()
    {
        WriteHistory("20260701-100000-aaaaaa", shortlist: [Match("l1", "Senior .NET Developer", "Great Co")]);
        var marks = Marks(("20260701-100000-aaaaaa", "l1", new ListingMark("good", null)));

        var example = Assert.Single(MarkedExamplesLoader.Load(_historyDir, marks));
        Assert.Equal("liked", example.Polarity);
        Assert.Null(example.Note);
    }

    [Fact]
    public void Load_FallsBackToShortlist_WhenNotInScored()
    {
        WriteHistory("20260701-100000-aaaaaa",
            scored: [Scored("other", "Other", "Other Co")],
            shortlist: [Match("l1", "Backend Developer", "Fallback Co")]);
        var marks = Marks(("20260701-100000-aaaaaa", "l1", new ListingMark("good", "great stack")));

        var example = Assert.Single(MarkedExamplesLoader.Load(_historyDir, marks));
        Assert.Equal("Fallback Co", example.Company);
        Assert.Equal("great stack", example.Note);
    }

    [Fact]
    public void Load_DuplicateAcrossRuns_NewestRunWins()
    {
        WriteHistory("20260601-100000-old", scored: [Scored("l1", "Backend Developer", "Same Co")]);
        WriteHistory("20260701-100000-new", scored: [Scored("l9", "Backend Developer", "Same Co")]);
        var marks = Marks(
            ("20260601-100000-old", "l1", new ListingMark("good", "old opinion")),
            ("20260701-100000-new", "l9", new ListingMark("bad", "new opinion")));

        var example = Assert.Single(MarkedExamplesLoader.Load(_historyDir, marks));
        Assert.Equal("disliked", example.Polarity);
        Assert.Equal("new opinion", example.Note);
    }

    [Fact]
    public void Load_MissingHistoryFile_IsSkipped()
    {
        var marks = Marks(("20260701-100000-gone", "l1", new ListingMark("bad", "whatever")));
        Assert.Empty(MarkedExamplesLoader.Load(_historyDir, marks));
    }

    [Fact]
    public void Load_UnknownListingId_IsSkipped()
    {
        WriteHistory("20260701-100000-aaaaaa", scored: [Scored("l1", "Backend Developer", "Some Co")]);
        var marks = Marks(("20260701-100000-aaaaaa", "no-such-listing", new ListingMark("bad", null)));

        Assert.Empty(MarkedExamplesLoader.Load(_historyDir, marks));
    }
}
