using System.Text.Json;
using Jobmatch.Json;
using Jobmatch.Models;
using Jobmatch.Search;
using Jobmatch.Services;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Services;

public sealed class ApplicationsServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly MarksService _marks;
    private readonly ApplicationsService _applications;

    public ApplicationsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-applications-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "apps@example.com", repoRoot: _tempRoot, seedExamples: false);
        _marks = new MarksService(_ctx);
        _applications = new ApplicationsService(_ctx, _marks);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
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
            Path.Combine(_ctx.HistoryDir, $"{runId}.json"),
            JsonSerializer.Serialize(detail, JobmatchJsonOptions.Indented));
    }

    private static ScoredEntry Scored(string id, string title, string company) =>
        new(id, title, company, "Copenhagen", "https://x/" + id, null, "portal", 0.8,
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0), ["C#"], []);

    private static ListingMatch Match(string id, string title, string company) =>
        new(id, "portal", title, company, "Aarhus", "onsite", "https://x/" + id, null, 0.7, "", [".NET"], []);

    [Fact]
    public void List_StatusOnlyAndMarkedEntries_AreAggregated()
    {
        WriteHistory("20260701-100000-aaaaaa", scored: [Scored("l1", "Role A", "Co A"), Scored("l2", "Role B", "Co B")]);
        _marks.SetStatus("20260701-100000-aaaaaa", "l1", "applied");
        _marks.Set("20260701-100000-aaaaaa", "l2", "good", "nice fit");
        _marks.SetStatus("20260701-100000-aaaaaa", "l2", "interview");

        var entries = _applications.List();
        Assert.Equal(2, entries.Count);
        var interview = entries.Single(e => e.Status == "interview");
        Assert.Equal("Role B", interview.Title);
        Assert.Equal("good", interview.Mark);
        Assert.Equal("nice fit", interview.Reason);
        var applied = entries.Single(e => e.Status == "applied");
        Assert.Null(applied.Mark);
    }

    [Fact]
    public void List_MarkWithoutStatus_IsExcluded()
    {
        WriteHistory("20260701-100000-aaaaaa", scored: [Scored("l1", "Role A", "Co A")]);
        _marks.Set("20260701-100000-aaaaaa", "l1", "good", null);

        Assert.Empty(_applications.List());
    }

    [Fact]
    public void List_SameListingInTwoRuns_NewestRunWins()
    {
        WriteHistory("20260601-100000-oldaaa", scored: [Scored("l1", "Role A", "Co A")]);
        WriteHistory("20260701-100000-newaaa", scored: [Scored("l1", "Role A", "Co A")]);
        _marks.SetStatus("20260601-100000-oldaaa", "l1", "applied");
        _marks.SetStatus("20260701-100000-newaaa", "l1", "interview");

        var entry = Assert.Single(_applications.List());
        Assert.Equal("interview", entry.Status);
        Assert.Equal("20260701-100000-newaaa", entry.RunId);
    }

    [Fact]
    public void List_MissingHistoryFile_IsSkipped()
    {
        _marks.SetStatus("20260701-100000-gonaaa", "l1", "applied");

        Assert.Empty(_applications.List());
    }

    [Fact]
    public void List_ResolvesFromShortlist_WhenNotInScored()
    {
        WriteHistory("20260701-100000-aaaaaa", shortlist: [Match("l1", "Role A", "Shortlist Co")]);
        _marks.SetStatus("20260701-100000-aaaaaa", "l1", "offer");

        var entry = Assert.Single(_applications.List());
        Assert.Equal("Shortlist Co", entry.Company);
        Assert.Equal(0.7, entry.Score);
    }

    [Fact]
    public void List_OrdersByStatusActivity_ThenNewestRun()
    {
        WriteHistory("20260701-100000-aaaaaa", scored:
            [Scored("l1", "A", "Co1"), Scored("l2", "B", "Co2"), Scored("l3", "C", "Co3"), Scored("l4", "D", "Co4")]);
        _marks.SetStatus("20260701-100000-aaaaaa", "l1", "rejected");
        _marks.SetStatus("20260701-100000-aaaaaa", "l2", "offer");
        _marks.SetStatus("20260701-100000-aaaaaa", "l3", "applied");
        _marks.SetStatus("20260701-100000-aaaaaa", "l4", "interview");

        var statuses = _applications.List().Select(e => e.Status).ToList();
        Assert.Equal(["offer", "interview", "applied", "rejected"], statuses);
    }
}
