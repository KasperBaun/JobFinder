using Hangfire;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Jobs;
using Jobmatch.Search;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Jobs;

/// <summary>
/// The background search. Hangfire invokes <see cref="Run"/> on a worker thread, decoupled from any HTTP
/// request, so the run survives client navigation / reload / host restart. It drives the
/// <see cref="ISearchService"/> pipeline, projects each progress event onto the persisted
/// <see cref="JobSearch"/> record + timeline, and publishes snapshots to the <see cref="JobSearchBus"/>
/// for live SSE viewers. The rich result <c>RunDetail</c> is written by the pipeline on success.
/// </summary>
[AutomaticRetry(Attempts = 1)]
public sealed class SearchJob(
    ISearchService search,
    IJobSearchStore store,
    JobSearchBus bus,
    ILogger<SearchJob> logger)
{
    private readonly object _gate = new();
    private JobSearch _job = null!;

    public async Task Run(string id, CancellationToken ct)
    {
        var job = store.Get(id);
        if (job is null)
        {
            logger.LogWarning("SearchJob invoked for unknown JobSearch id {Id}; skipping", id);
            return;
        }
        if (job.IsTerminal)
        {
            logger.LogInformation("SearchJob {Id} already terminal ({State}); skipping", id, job.State);
            return;
        }

        Persist(job.MarkRunning(DateTimeOffset.UtcNow), publish: true);
        logger.LogInformation("Search run {Id} started", id);

        using var heartbeat = StartHeartbeat(ct);
        try
        {
            await foreach (var evt in search.RunAsync(_job.Request, id, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                Persist(Apply(_job, evt), publish: true);
            }

            Persist(_job.MarkSucceeded(_job.ShortlistCount, _job.TopScore, DateTimeOffset.UtcNow), publish: true);
            logger.LogInformation("Search run {Id} succeeded — {Count} top jobs", id, _job.ShortlistCount);
        }
        catch (OperationCanceledException)
        {
            var latest = store.Get(id) ?? _job;
            if (!latest.IsTerminal)
                Persist(latest.MarkCancelled(DateTimeOffset.UtcNow), publish: true);
            logger.LogWarning("Search run {Id} cancelled", id);
            throw;
        }
        catch (Exception ex)
        {
            Persist(_job.MarkFailed(ex.Message, DateTimeOffset.UtcNow), publish: true);
            logger.LogError(ex, "Search run {Id} failed", id);
            throw;
        }
    }

    private JobSearch Apply(JobSearch job, SearchProgressEvent evt)
    {
        var now = DateTimeOffset.UtcNow;
        switch (evt)
        {
            case StartedEvent s:
                return job.Log(JobSearchEventLevel.Info, JobSearchPhase.Fetching, $"Fetching listings from {s.Total} sources", now);

            case ProviderRunningEvent:
            case ProviderDoneEvent:
            case ProviderFailedEvent:
                return ApplyProvider(job, evt, now);

            case DedupeEvent d:
                return job
                    .WithCounts(deduped: d.MergedCount)
                    .Log(JobSearchEventLevel.Info, JobSearchPhase.Deduping, $"{d.MergedCount} unique jobs after removing duplicates", now, count: d.MergedCount);

            case LlmJudgingEvent l:
                return job.Log(JobSearchEventLevel.Info, JobSearchPhase.LlmJudging, $"AI reviewing top {l.Total} jobs…", now, count: l.Total);

            case RankEvent r:
                return job
                    .WithCounts(ranked: r.RankedCount, shortlist: r.RankedCount, topScore: r.TopScore)
                    .Log(JobSearchEventLevel.Info, JobSearchPhase.Ranking, $"{r.RankedCount} jobs rated · best {r.TopScore:0.00}", now, count: r.RankedCount);

            case CompleteEvent c:
                return job
                    .WithCounts(shortlist: c.Shortlist.Count, topScore: c.Shortlist.Count > 0 ? c.Shortlist[0].Score : job.TopScore)
                    .Log(JobSearchEventLevel.Info, JobSearchPhase.Writing, "Writing results", now, count: c.Shortlist.Count);

            case ErrorEvent e:
                throw new InvalidOperationException(e.Message);

            default:
                return job;
        }
    }

    private static JobSearch ApplyProvider(JobSearch job, SearchProgressEvent evt, DateTimeOffset now)
    {
        switch (evt)
        {
            case ProviderRunningEvent p:
                return job
                    .WithProviders(Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Running, null, null)))
                    .Log(JobSearchEventLevel.Info, JobSearchPhase.Fetching, $"Fetching {p.Provider} ({p.Index}/{p.Total})", now, p.Provider);

            case ProviderDoneEvent p:
            {
                var providers = Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Ok, p.FetchedCount, null, p.DurationMs, p.HitPageCap, p.PossiblyCapped));
                var withCounts = job.WithProviders(providers).WithCounts(fetched: SumOk(providers));
                var capNote = p.HitPageCap ? " · hit page cap, may be more"
                    : p.PossiblyCapped ? " · at configured limit, may be more"
                    : "";
                var level = p.HitPageCap || p.PossiblyCapped ? JobSearchEventLevel.Warn : JobSearchEventLevel.Info;
                return withCounts.Log(level, JobSearchPhase.Fetching, $"{p.Provider}: {p.FetchedCount} jobs · {p.DurationMs / 1000.0:0}s{capNote}", now, p.Provider, p.FetchedCount, p.DurationMs);
            }

            case ProviderFailedEvent p:
                return job
                    .WithProviders(Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Failed, null, p.Error, p.DurationMs)))
                    .Log(JobSearchEventLevel.Warn, JobSearchPhase.Fetching, $"{p.Provider} failed: {p.Error} · {p.DurationMs / 1000.0:0}s", now, p.Provider, durationMs: p.DurationMs);

            default:
                return job;
        }
    }

    private IDisposable StartHeartbeat(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_job.IsTerminal) break;
                        _job = _job.Heartbeat(DateTimeOffset.UtcNow);
                        store.Save(_job);
                    }
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
        }, cts.Token);
        return cts;
    }

    private void Persist(JobSearch job, bool publish)
    {
        lock (_gate)
        {
            _job = job;
            store.Save(job);
        }
        if (publish) bus.Publish(job);
    }

    private static IReadOnlyList<ProviderRunStatus> Upsert(IReadOnlyList<ProviderRunStatus> providers, ProviderRunStatus update)
    {
        var list = new List<ProviderRunStatus>(providers.Count + 1);
        var replaced = false;
        foreach (var p in providers)
        {
            if (string.Equals(p.Name, update.Name, StringComparison.Ordinal))
            {
                list.Add(update);
                replaced = true;
            }
            else
            {
                list.Add(p);
            }
        }
        if (!replaced) list.Add(update);
        return list;
    }

    private static int SumOk(IReadOnlyList<ProviderRunStatus> providers) =>
        providers.Where(p => p.Status == ProviderRunState.Ok).Sum(p => p.FetchedCount ?? 0);
}
