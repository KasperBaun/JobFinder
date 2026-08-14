using Hangfire;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Jobs;
using Jobmatch.Pipeline;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Features.Search;

/// <summary>
/// The background search. Hangfire invokes <see cref="Run"/> on a worker thread, decoupled from any HTTP
/// request, so the run survives client navigation / reload / host restart. It drives the
/// <see cref="ISearchService"/> pipeline, projects each progress event onto the persisted
/// <see cref="JobSearch"/> record + timeline, and publishes snapshots to the <see cref="JobSearchBus"/>
/// for live SSE viewers. The rich result <c>RunDetail</c> is written by the pipeline on success.
/// </summary>
[AutomaticRetry(Attempts = 1)]
public sealed partial class SearchJob(
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
