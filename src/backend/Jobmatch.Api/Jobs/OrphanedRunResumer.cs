using Hangfire;
using Jobmatch;
using Jobmatch.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Jobs;

/// <summary>
/// Runs once at startup, before the Hangfire server begins dequeuing. A run left in the <c>running</c>
/// state on disk was mid-flight when the previous process exited (host killed / dev-server restarted).
/// Hangfire's durable queue would eventually re-run it, but only after the SQLite invisibility timeout
/// (~30 min), which reads as a stuck run. This re-enqueues those runs immediately so they resume
/// promptly (R-036) — the worker re-drives the pipeline from the start and <see cref="JobSearch.MarkRunning"/>
/// records it as a resumed attempt. Queued runs are left alone (Hangfire starts them from its own queue);
/// a run that cannot resume still surfaces as interrupted via the store's stale-heartbeat reconcile (R-055).
/// </summary>
public sealed class OrphanedRunResumer(
    IServiceScopeFactory scopes,
    IBackgroundJobClient hangfire,
    ILogger<OrphanedRunResumer> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobSearchStore>();
            var count = Resume(store, hangfire, DateTimeOffset.UtcNow);
            if (count > 0)
                logger.LogInformation("Re-enqueued {Count} interrupted search run(s) to resume on startup", count);
        }
        catch (SetupRequiredException)
        {
            // No data directory chosen yet (first run) — there is nothing to resume.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup orphaned-run resume failed");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static int Resume(IJobSearchStore store, IBackgroundJobClient hangfire, DateTimeOffset now)
    {
        var count = 0;
        foreach (var job in store.ListPersisted())
        {
            if (job.State != JobSearchState.Running) continue;

            // Drop the old job (its worker is gone, stuck Processing until the invisibility timeout) and
            // enqueue a fresh one. Refresh the heartbeat so the run stays surfaced as active in the gap
            // before the worker picks it up, rather than momentarily reconciling to interrupted.
            if (job.HangfireJobId is not null) hangfire.Delete(job.HangfireJobId);
            var newJobId = hangfire.Enqueue<SearchJob>(j => j.Run(job.Id, CancellationToken.None));
            store.Save(job.WithHangfireJobId(newJobId).Heartbeat(now));
            count++;
        }
        return count;
    }
}
