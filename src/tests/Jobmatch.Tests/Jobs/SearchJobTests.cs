using System.Runtime.CompilerServices;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Jobs;
using Jobmatch.Jobs;
using Jobmatch.Search;
using Microsoft.Extensions.Logging.Abstractions;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Jobs;

public sealed class SearchJobTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly JobSearchStore _store;
    private readonly JobSearchBus _bus = new();

    public SearchJobTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-searchjob-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "job@example.com", repoRoot: _tempRoot, seedExamples: false);
        _store = new JobSearchStore(_ctx);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private string Seed()
    {
        var id = RunId.New(DateTimeOffset.UtcNow);
        _store.Save(JobSearch.Create(id, new SearchRequest(), DateTimeOffset.UtcNow));
        return id;
    }

    private static ListingMatch Match(string id, double score) =>
        new(id, "p", "Title " + id, null, null, "remote", "https://x/" + id, null, score, "", [], []);

    [Fact]
    public async Task Run_Maps_Events_To_Succeeded_With_Counts_And_Providers()
    {
        var id = Seed();
        var events = new SearchProgressEvent[]
        {
            new StartedEvent(id, 1),
            new ProviderRunningEvent("p", 1, 1),
            new ProviderDoneEvent("p", 5, 1, 1),
            new DedupeEvent(4),
            new RankEvent(2, 0.7),
            new CompleteEvent(id, [Match("a", 0.7), Match("b", 0.6)]),
        };
        var job = new SearchJob(new FakeSearchService(events), _store, _bus, NullLogger<SearchJob>.Instance);

        await job.Run(id, CancellationToken.None);

        var result = _store.Get(id)!;
        Assert.Equal(JobSearchState.Succeeded, result.State);
        Assert.Equal(2, result.ShortlistCount);
        Assert.Equal(0.7, result.TopScore);
        Assert.Equal(4, result.DedupedCount);
        Assert.Equal(5, result.FetchedCount);
        var p = Assert.Single(result.Providers);
        Assert.Equal(ProviderRunState.Ok, p.Status);
        Assert.Equal(5, p.FetchedCount);
    }

    [Fact]
    public async Task Run_Records_Provider_Failure_But_Still_Completes()
    {
        var id = Seed();
        var events = new SearchProgressEvent[]
        {
            new StartedEvent(id, 1),
            new ProviderRunningEvent("p", 1, 1),
            new ProviderFailedEvent("p", "timeout", 1, 1),
            new DedupeEvent(0),
            new RankEvent(0, 0.0),
            new CompleteEvent(id, []),
        };
        var job = new SearchJob(new FakeSearchService(events), _store, _bus, NullLogger<SearchJob>.Instance);

        await job.Run(id, CancellationToken.None);

        var result = _store.Get(id)!;
        Assert.Equal(JobSearchState.Succeeded, result.State);
        Assert.Equal(ProviderRunState.Failed, Assert.Single(result.Providers).Status);
    }

    [Fact]
    public async Task Run_Marks_Failed_When_Pipeline_Throws()
    {
        var id = Seed();
        var fake = new FakeSearchService([new StartedEvent(id, 0)], throwAfter: new InvalidOperationException("kaboom"));
        var job = new SearchJob(fake, _store, _bus, NullLogger<SearchJob>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.Run(id, CancellationToken.None));

        var result = _store.Get(id)!;
        Assert.Equal(JobSearchState.Failed, result.State);
    }

    [Fact]
    public async Task Run_Marks_Cancelled_On_OperationCanceled()
    {
        var id = Seed();
        var fake = new FakeSearchService([new StartedEvent(id, 1)], throwAfter: new OperationCanceledException());
        var job = new SearchJob(fake, _store, _bus, NullLogger<SearchJob>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => job.Run(id, CancellationToken.None));

        Assert.Equal(JobSearchState.Cancelled, _store.Get(id)!.State);
    }

    [Fact]
    public async Task Run_Skips_Unknown_Id()
    {
        var job = new SearchJob(new FakeSearchService([]), _store, _bus, NullLogger<SearchJob>.Instance);
        await job.Run("does-not-exist", CancellationToken.None); // must not throw
    }

    private sealed class FakeSearchService(IReadOnlyList<SearchProgressEvent> events, Exception? throwAfter = null) : ISearchService
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
            if (throwAfter is not null) throw throwAfter;
        }
    }
}
