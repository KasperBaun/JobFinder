using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jobmatch.Api.Jobs;
using Jobmatch.Jobs;
using Jobmatch.Search;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

public sealed class JobSearchServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobSearchStore _store;
    private readonly FakeBackgroundJobClient _hangfire = new();
    private readonly JobSearchService _service;

    public JobSearchServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-jobsvc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        var ctx = JobmatchUserContext.Resolve(emailOverride: "svc@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(ctx);
        _service = new JobSearchService(_store, _hangfire);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Create_Persists_Queued_Run_And_Enqueues_It()
    {
        var job = _service.Create(new SearchRequest());

        Assert.Equal(JobSearchState.Queued, job.State);
        Assert.Equal("hf-1", job.HangfireJobId);
        Assert.Equal(nameof(SearchJob.Run), _hangfire.LastJob?.Method.Name);

        var stored = _store.Get(job.Id)!;
        Assert.Equal(JobSearchState.Queued, stored.State);
        Assert.Equal("hf-1", stored.HangfireJobId);
    }

    [Fact]
    public void Cancel_Deletes_The_Hangfire_Job_And_Marks_Cancelled()
    {
        var job = _service.Create(new SearchRequest());

        _service.Cancel(job.Id);

        Assert.Contains("hf-1", _hangfire.Deleted);
        Assert.Equal(JobSearchState.Cancelled, _store.Get(job.Id)!.State);
    }

    [Fact]
    public void Cancel_Is_Noop_For_Unknown_Or_Terminal_Runs()
    {
        _service.Cancel("unknown"); // no throw
        Assert.Empty(_hangfire.Deleted);
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public readonly List<string> Deleted = [];
        public Job? LastJob { get; private set; }

        public string Create(Job job, IState state)
        {
            LastJob = job;
            return "hf-1";
        }

        public bool ChangeState(string jobId, IState state, string? expectedState)
        {
            if (state is DeletedState) Deleted.Add(jobId);
            return true;
        }
    }
}
