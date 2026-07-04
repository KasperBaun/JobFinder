using Jobmatch.Jobs;
using Jobmatch.Search;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

public sealed class JobSearchStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly JobSearchStore _store;

    public JobSearchStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-jobstore-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "store@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(_ctx);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static JobSearch Job(string id, DateTimeOffset created) =>
        JobSearch.Create(id, new SearchRequest(), created);

    [Fact]
    public void Save_Then_Get_RoundTrips()
    {
        var job = Job("20260605-120000-aaaaaa", DateTimeOffset.UtcNow).MarkRunning(DateTimeOffset.UtcNow);
        _store.Save(job);

        var read = _store.Get(job.Id);
        Assert.NotNull(read);
        Assert.Equal(job.Id, read!.Id);
        Assert.Equal(JobSearchState.Running, read.State);
        Assert.Equal(JobSearchPhase.Fetching, read.Phase);
    }

    [Fact]
    public void List_Orders_Newest_First()
    {
        var older = Job("20260605-120000-aaaaaa", DateTimeOffset.UtcNow.AddHours(-2));
        var newer = Job("20260605-140000-bbbbbb", DateTimeOffset.UtcNow.AddHours(-1));
        _store.Save(older);
        _store.Save(newer);

        var list = _store.List();
        Assert.Equal(2, list.Count);
        Assert.Equal(newer.Id, list[0].Id);
    }

    [Fact]
    public void Active_Returns_Non_Terminal_Run()
    {
        var done = Job("20260605-120000-aaaaaa", DateTimeOffset.UtcNow.AddHours(-1)).MarkRunning(DateTimeOffset.UtcNow).MarkSucceeded(1, 0.5, DateTimeOffset.UtcNow);
        var running = Job("20260605-140000-bbbbbb", DateTimeOffset.UtcNow).MarkRunning(DateTimeOffset.UtcNow);
        _store.Save(done);
        _store.Save(running);

        Assert.Equal(running.Id, _store.Active()!.Id);
    }

    [Fact]
    public void Get_Reconciles_Stale_Running_To_Interrupted()
    {
        var stale = Job("20260605-120000-cccccc", DateTimeOffset.UtcNow)
            .MarkRunning(DateTimeOffset.UtcNow) with
        { LastHeartbeat = DateTimeOffset.UtcNow - JobSearchStore.StaleAfter - TimeSpan.FromMinutes(1) };
        _store.Save(stale);

        var read = _store.Get(stale.Id);
        Assert.Equal(JobSearchState.Interrupted, read!.State);
        Assert.True(read.IsTerminal);
    }

    [Fact]
    public void Get_Does_Not_Reconcile_Fresh_Running()
    {
        var fresh = Job("20260605-120000-dddddd", DateTimeOffset.UtcNow).MarkRunning(DateTimeOffset.UtcNow);
        _store.Save(fresh);
        Assert.Equal(JobSearchState.Running, _store.Get(fresh.Id)!.State);
    }

    [Fact]
    public void Delete_Removes_The_File()
    {
        var job = Job("20260605-120000-eeeeee", DateTimeOffset.UtcNow);
        _store.Save(job);
        Assert.Equal(1, _store.Delete([job.Id]));
        Assert.Null(_store.Get(job.Id));
    }

    [Fact]
    public void Get_Rejects_Path_Traversal_Ids()
    {
        Assert.Null(_store.Get("../escape"));
        Assert.Null(_store.Get("a/b"));
    }
}
