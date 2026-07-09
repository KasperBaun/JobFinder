using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Jobs;
using Jobmatch.Jobs;
using Jobmatch.Search;
using Microsoft.Extensions.Logging.Abstractions;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

/// <summary>
/// Integration-level coverage for the durability story the background-jobs refactor exists to provide:
/// a search runs decoupled from any HTTP request, survives reconnect, streams live snapshots, and is
/// cancellable. Unlike the per-component unit tests, this wires the REAL <see cref="JobSearchStore"/>,
/// <see cref="JobSearchBus"/>, <see cref="JobSearchService"/> and <see cref="SearchJob"/> together and
/// drives them through one realistic event sequence — only the Hangfire client and the search pipeline
/// (an external integration) are faked.
/// </summary>
public sealed class SearchDurabilityTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobSearchStore _store;
    private readonly JobSearchBus _bus = new();
    private readonly FakeBackgroundJobClient _hangfire = new();
    private readonly JobSearchService _service;

    public SearchDurabilityTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-durability-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        var ctx = JobmatchUserContext.Resolve(emailOverride: "durability@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(ctx);
        _service = new JobSearchService(_store, _hangfire);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Started → two providers (one ok, one failed) → dedupe → rank → complete: the shape a real run emits.
    private static SearchProgressEvent[] RealisticRun(string id) =>
    [
        new StartedEvent(id, 2),
        new ProviderRunningEvent("greenhouse", 1, 2),
        new ProviderDoneEvent("greenhouse", 7, 1, 2, 900),
        new ProviderRunningEvent("thehub", 2, 2),
        new ProviderFailedEvent("thehub", "connection timed out", 2, 2, 1500),
        new DedupeEvent(5),
        new RankEvent(3, 0.82),
        new CompleteEvent(id, [Match("a", 0.82), Match("b", 0.71), Match("c", 0.55)]),
    ];

    private static ListingMatch Match(string id, double score) =>
        new(id, "p", "Title " + id, null, null, "remote", "https://x/" + id, null, score, "", [], []);

    private SearchJob NewJob(IReadOnlyList<SearchProgressEvent> events) =>
        new(new FakeSearchService(events), _store, _bus, NullLogger<SearchJob>.Instance);

    // 1. Enqueue → run → terminal: Create persists Queued + enqueues; running the job drives it to
    //    Succeeded with the final counts/phase persisted in the store.
    [Fact]
    public async Task Enqueue_Then_Run_Drives_Persisted_Run_To_Succeeded()
    {
        var queued = _service.Create(new SearchRequest());

        Assert.Equal(JobSearchState.Queued, queued.State);
        Assert.Equal("hf-1", queued.HangfireJobId);
        Assert.Equal(nameof(SearchJob.Run), _hangfire.LastJob!.Method.Name);

        await NewJob(RealisticRun(queued.Id)).Run(queued.Id, CancellationToken.None);

        var final = _store.Get(queued.Id)!;
        Assert.Equal(JobSearchState.Succeeded, final.State);
        Assert.Equal(JobSearchPhase.Done, final.Phase);
        Assert.Equal(3, final.ShortlistCount);
        Assert.Equal(0.82, final.TopScore);
        Assert.Equal(5, final.DedupedCount);
        Assert.Equal(7, final.FetchedCount);
        Assert.NotNull(final.FinishedAt);
    }

    // 2. Live SSE snapshots: a viewer subscribed BEFORE the run receives an ordered progression that
    //    ends in a terminal state. Draining concurrently mirrors a real SSE viewer and avoids the
    //    bounded-channel DropOldest evicting early snapshots.
    [Fact]
    public async Task Subscriber_Receives_Ordered_Snapshots_Ending_Terminal()
    {
        var id = _service.Create(new SearchRequest()).Id;
        using var sub = _bus.Subscribe(id);

        var drain = DrainAsync(sub.Reader);
        await NewJob(RealisticRun(id)).Run(id, CancellationToken.None);

        var snapshots = await drain;

        Assert.NotEmpty(snapshots);
        Assert.Equal(JobSearchState.Running, snapshots[0].State);
        Assert.Equal(JobSearchState.Succeeded, snapshots[^1].State);
        Assert.True(snapshots[^1].IsTerminal);
        // Exactly one terminal snapshot (the bus completes the channel on terminal).
        Assert.Equal(1, snapshots.Count(s => s.IsTerminal));
        // Monotonic phase progression up to Done.
        var phases = snapshots.Select(s => (int)s.Phase).ToList();
        Assert.Equal(phases.OrderBy(p => p), phases);
    }

    // 3. Reconnect: while non-terminal, both the store and the service surface the run as Active so a
    //    reloaded client can re-attach; once terminal it is no longer Active.
    [Fact]
    public async Task Active_Surfaces_Run_For_Reconnect_Until_Terminal()
    {
        var id = _service.Create(new SearchRequest()).Id;

        Assert.Equal(id, _store.Active()!.Id);
        Assert.Equal(id, _service.Active()!.Id);

        await NewJob(RealisticRun(id)).Run(id, CancellationToken.None);

        Assert.Null(_store.Active());
        Assert.Null(_service.Active());
        Assert.Equal(JobSearchState.Succeeded, _store.Get(id)!.State);
    }

    // 4. Cancel: cancelling a queued/running run removes the Hangfire job and marks it Cancelled.
    [Fact]
    public void Cancel_Removes_Hangfire_Job_And_Marks_Cancelled()
    {
        var id = _service.Create(new SearchRequest()).Id;

        _service.Cancel(id);

        Assert.Contains("hf-1", _hangfire.Deleted);
        var cancelled = _store.Get(id)!;
        Assert.Equal(JobSearchState.Cancelled, cancelled.State);
        Assert.True(cancelled.IsTerminal);
        Assert.Null(_service.Active());
    }

    // 5. Provider failure does not abort the run: the ProviderFailedEvent is recorded on the failing
    //    provider, yet the run still reaches Succeeded with the surviving provider's counts.
    [Fact]
    public async Task Provider_Failure_Is_Recorded_But_Run_Still_Succeeds()
    {
        var id = _service.Create(new SearchRequest()).Id;

        await NewJob(RealisticRun(id)).Run(id, CancellationToken.None);

        var final = _store.Get(id)!;
        Assert.Equal(JobSearchState.Succeeded, final.State);

        var failed = final.Providers.Single(p => p.Name == "thehub");
        Assert.Equal(ProviderRunState.Failed, failed.Status);
        Assert.Equal("connection timed out", failed.Error);

        var ok = final.Providers.Single(p => p.Name == "greenhouse");
        Assert.Equal(ProviderRunState.Ok, ok.Status);
        Assert.Equal(7, ok.FetchedCount);

        Assert.Contains(final.Timeline, e => e.Level == JobSearchEventLevel.Warn && e.Message.Contains("thehub failed"));
    }

    private static async Task<List<JobSearch>> DrainAsync(ChannelReader<JobSearch> reader)
    {
        using var cts = new CancellationTokenSource(Timeout);
        var received = new List<JobSearch>();
        await foreach (var snapshot in reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
            received.Add(snapshot);
        return received;
    }

    private sealed class FakeSearchService(IReadOnlyList<SearchProgressEvent> events) : ISearchService
    {
        public IAsyncEnumerable<SearchProgressEvent> RunAsync(SearchRequest req, CancellationToken ct = default)
            => RunAsync(req, "x", ct);

        public async IAsyncEnumerable<SearchProgressEvent> RunAsync(
            SearchRequest req,
            string runId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var e in events)
            {
                ct.ThrowIfCancellationRequested();
                yield return e;
                await Task.Yield();
            }
        }
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
