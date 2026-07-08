using Jobmatch;
using Jobmatch.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Jobs;

/// <summary>
/// Runs once at startup, before the Hangfire server begins dequeuing. A freshly-started process cannot
/// be the worker for a run the store still surfaces as Running, so any such run was orphaned when the
/// previous process exited (host killed / dev-server restarted mid-run). Persists those as Interrupted
/// immediately (R-055) instead of leaving the GUI on a climbing "running" timer until the read-time
/// stale reconcile fires (<see cref="JobSearchStore.StaleAfter"/>). Queued runs are left untouched so
/// Hangfire can still start work enqueued before the restart (R-036).
/// </summary>
public sealed class OrphanedRunReconciler(IServiceScopeFactory scopes, ILogger<OrphanedRunReconciler> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobSearchStore>();
            var count = Reconcile(store, DateTimeOffset.UtcNow);
            if (count > 0)
                logger.LogInformation("Marked {Count} orphaned search run(s) as interrupted on startup", count);
        }
        catch (SetupRequiredException)
        {
            // No data directory chosen yet (first run) — there is nothing to reconcile.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup orphaned-run reconciliation failed");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static int Reconcile(IJobSearchStore store, DateTimeOffset now)
    {
        var count = 0;
        foreach (var job in store.List())
        {
            if (job.State != JobSearchState.Running) continue;
            store.Save(job.AsInterrupted(now));
            count++;
        }
        return count;
    }
}
