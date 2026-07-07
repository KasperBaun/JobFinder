using System.Text.Json;
using Jobmatch.Jobs;
using Jobmatch.Json;
using Jobmatch.Search;
using Jobmatch.Services;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Services;

public sealed class HistoryServiceMergeTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly JobSearchStore _store;
    private readonly HistoryService _history;

    public HistoryServiceMergeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-history-merge-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "hist@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(_ctx);
        _history = new HistoryService(_ctx, new MarksService(_ctx), _store);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private void WriteLegacyHistory(string runId, DateTimeOffset startedAt)
    {
        var detail = new RunDetail(
            RunId: runId,
            StartedAt: startedAt,
            Providers: [new ProviderRunStatus("p", ProviderRunState.Ok, 3, null)],
            FetchedCount: 3,
            DedupedCount: 3,
            RankedCount: 1,
            ShortlistCount: 1,
            TopScore: 0.8,
            GoodMarks: 0,
            Shortlist: [new ListingMatch("l1", "p", "Job", null, null, "remote", "https://x/1", null, 0.8, "", [], [])],
            Marks: new Dictionary<string, string>());
        File.WriteAllText(
            Path.Combine(_ctx.HistoryDir, $"{runId}.json"),
            JsonSerializer.Serialize(detail, JobmatchJsonOptions.Indented));
    }

    [Fact]
    public void List_Includes_JobSearch_Runs_With_State()
    {
        var running = JobSearch.Create("20260605-130000-aaaaaa", new SearchRequest(), DateTimeOffset.UtcNow)
            .MarkRunning(DateTimeOffset.UtcNow);
        _store.Save(running);

        var runs = _history.List();
        var summary = Assert.Single(runs);
        Assert.Equal(running.Id, summary.RunId);
        Assert.Equal(JobSearchState.Running, summary.State);
    }

    [Fact]
    public void List_Includes_Legacy_History_As_Succeeded()
    {
        WriteLegacyHistory("20260101-090000-legacy", DateTimeOffset.UtcNow.AddDays(-1));

        var summary = Assert.Single(_history.List());
        Assert.Equal("20260101-090000-legacy", summary.RunId);
        Assert.Equal(JobSearchState.Succeeded, summary.State);
        Assert.Equal(1, summary.ShortlistCount);
    }

    [Fact]
    public void List_Does_Not_Duplicate_A_Run_With_Both_Records()
    {
        var id = "20260605-150000-bothbb";
        _store.Save(JobSearch.Create(id, new SearchRequest(), DateTimeOffset.UtcNow)
            .MarkRunning(DateTimeOffset.UtcNow)
            .MarkSucceeded(1, 0.8, DateTimeOffset.UtcNow));
        WriteLegacyHistory(id, DateTimeOffset.UtcNow);

        var runs = _history.List();
        Assert.Single(runs);
        Assert.Equal(JobSearchState.Succeeded, runs[0].State);
    }

    [Fact]
    public void GetByRunId_Returns_Rich_Detail_For_Succeeded_Run()
    {
        var id = "20260605-160000-richaa";
        _store.Save(JobSearch.Create(id, new SearchRequest(), DateTimeOffset.UtcNow)
            .MarkRunning(DateTimeOffset.UtcNow)
            .MarkSucceeded(1, 0.8, DateTimeOffset.UtcNow));
        WriteLegacyHistory(id, DateTimeOffset.UtcNow);

        var detail = _history.GetByRunId(id);
        Assert.Equal(JobSearchState.Succeeded, detail.State);
        Assert.Single(detail.Shortlist);
        Assert.NotNull(detail.Timeline);
    }

    [Fact]
    public void GetByRunId_Synthesises_Detail_For_Running_Run_Without_History()
    {
        var running = JobSearch.Create("20260605-170000-runaaa", new SearchRequest(), DateTimeOffset.UtcNow)
            .MarkRunning(DateTimeOffset.UtcNow);
        _store.Save(running);

        var detail = _history.GetByRunId(running.Id);
        Assert.Equal(JobSearchState.Running, detail.State);
        Assert.Empty(detail.Shortlist);
        Assert.NotNull(detail.Timeline);
        Assert.NotEmpty(detail.Timeline!);
    }

    [Fact]
    public void GetByRunId_Throws_NotFound_For_Unknown()
    {
        Assert.Throws<Jobmatch.NotFoundException>(() => _history.GetByRunId("does-not-exist"));
    }

    [Fact]
    public void GetByRunId_StatusOnlyEntry_PopulatesMarkStatuses_NotMarks()
    {
        var id = "20260605-180000-stataa";
        WriteLegacyHistory(id, DateTimeOffset.UtcNow);
        var marks = new MarksService(_ctx);
        marks.SetStatus(id, "l1", "applied");

        var detail = _history.GetByRunId(id);
        Assert.NotNull(detail.MarkStatuses);
        Assert.Equal("applied", detail.MarkStatuses!["l1"]);
        Assert.Empty(detail.Marks);
        Assert.Equal(0, detail.GoodMarks);
    }
}
