using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jobmatch.Api.Jobs;
using Jobmatch.Jobs;
using Jobmatch.Search;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

/// <summary>
/// The startup resumer: a run left <c>running</c> when the previous process exited is orphaned (its
/// Hangfire worker is gone). Rather than wait out Hangfire's invisibility timeout, the resumer drops the
/// stale job and re-enqueues a fresh one so the run resumes promptly (R-036). Queued and terminal runs
/// are untouched. It reads the raw persisted state, so even a long-stale run (which the store's read-time
/// reconcile would surface as interrupted) is still resumed.
/// </summary>
public sealed class OrphanedRunResumerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobSearchStore _store;

    public OrphanedRunResumerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-resume-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        var ctx = JobmatchUserContext.Resolve(emailOverride: "resume@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(ctx);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Reenqueues_Running_Run_And_Leaves_Queued_And_Terminal()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Save(JobSearch.Create("run-running", new SearchRequest(), now).MarkRunning(now).WithHangfireJobId("hf-old"));
        _store.Save(JobSearch.Create("run-queued", new SearchRequest(), now));
        _store.Save(JobSearch.Create("run-done", new SearchRequest(), now).MarkRunning(now).MarkSucceeded(3, 0.8, now));
        var hangfire = new FakeBackgroundJobClient();

        var count = OrphanedRunResumer.Resume(_store, hangfire, now.AddSeconds(1));

        Assert.Equal(1, count);
        Assert.Contains("hf-old", hangfire.Deleted);
        Assert.Equal(nameof(SearchJob.Run), hangfire.LastJob!.Method.Name);

        var resumed = _store.Get("run-running")!;
        Assert.False(resumed.IsTerminal);
        Assert.Equal("hf-1", resumed.HangfireJobId);
        Assert.Equal(JobSearchState.Queued, _store.Get("run-queued")!.State);
        Assert.Equal(JobSearchState.Succeeded, _store.Get("run-done")!.State);
    }

    [Fact]
    public void Resumes_Even_When_Heartbeat_Is_Long_Stale()
    {
        var old = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30);
        _store.Save(JobSearch.Create("run-stale", new SearchRequest(), old).MarkRunning(old));
        var hangfire = new FakeBackgroundJobClient();

        var count = OrphanedRunResumer.Resume(_store, hangfire, DateTimeOffset.UtcNow);

        Assert.Equal(1, count);
        Assert.Equal(nameof(SearchJob.Run), hangfire.LastJob!.Method.Name);
    }

    [Fact]
    public void Is_NoOp_When_No_Runs_Exist()
    {
        Assert.Equal(0, OrphanedRunResumer.Resume(_store, new FakeBackgroundJobClient(), DateTimeOffset.UtcNow));
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public readonly List<string> Deleted = [];
        public Job? LastJob { get; private set; }
        private int _n;

        public string Create(Job job, IState state)
        {
            LastJob = job;
            return "hf-" + ++_n;
        }

        public bool ChangeState(string jobId, IState state, string? expectedState)
        {
            if (state is DeletedState) Deleted.Add(jobId);
            return true;
        }
    }
}
