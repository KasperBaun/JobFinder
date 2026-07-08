using Jobmatch.Api.Jobs;
using Jobmatch.Jobs;
using Jobmatch.Search;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

/// <summary>
/// The startup reconciler: a freshly-started process cannot be the worker for a run the store still
/// surfaces as Running, so those are orphaned (host killed mid-run) and get persisted as Interrupted
/// immediately (R-055) — rather than lingering as a climbing "running" timer until the read-time
/// stale reconcile fires. Queued runs are left for Hangfire to start (R-036); terminal runs untouched.
/// </summary>
public sealed class OrphanedRunReconcilerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobSearchStore _store;

    public OrphanedRunReconcilerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-orphan-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        var ctx = JobmatchUserContext.Resolve(emailOverride: "orphan@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(ctx);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Persists_Interrupted_For_Running_Run_And_Leaves_Queued_And_Terminal()
    {
        var now = DateTimeOffset.UtcNow;
        // Fresh heartbeat so the store's read-time stale reconcile does NOT already flip it — this
        // is exactly the fast-restart case where the run still looks Running on disk.
        _store.Save(JobSearch.Create("run-running", new SearchRequest(), now).MarkRunning(now));
        _store.Save(JobSearch.Create("run-queued", new SearchRequest(), now));
        _store.Save(JobSearch.Create("run-done", new SearchRequest(), now).MarkRunning(now).MarkSucceeded(3, 0.8, now));

        var count = OrphanedRunReconciler.Reconcile(_store, now.AddSeconds(1));

        Assert.Equal(1, count);
        Assert.Equal(JobSearchState.Interrupted, _store.Get("run-running")!.State);
        Assert.NotNull(_store.Get("run-running")!.FinishedAt);
        Assert.Equal(JobSearchState.Queued, _store.Get("run-queued")!.State);
        Assert.Equal(JobSearchState.Succeeded, _store.Get("run-done")!.State);
    }

    [Fact]
    public void Is_NoOp_When_No_Runs_Exist()
    {
        Assert.Equal(0, OrphanedRunReconciler.Reconcile(_store, DateTimeOffset.UtcNow));
    }
}
