using Hangfire;
using Jobmatch.Jobs;
using Jobmatch.Search;

namespace Jobmatch.Api.Jobs;

public interface IJobSearchService
{
    JobSearch Create(SearchRequest request);
    JobSearch? Get(string id);
    JobSearch? Active();
    void Cancel(string id);
}

/// <summary>
/// Creates and controls background search runs. <see cref="Create"/> persists a Queued
/// <see cref="JobSearch"/> and enqueues a Hangfire job; the worker (<see cref="SearchJob"/>) drives it
/// to completion. Reads go through the store (which reconciles stale runs to interrupted).
/// </summary>
public sealed class JobSearchService(IJobSearchStore store, IBackgroundJobClient hangfire) : IJobSearchService
{
    public JobSearch Create(SearchRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var id = RunId.New(now);

        store.Save(JobSearch.Create(id, request, now));

        // Hangfire substitutes the real job CancellationToken for CancellationToken.None at runtime.
        var hangfireJobId = hangfire.Enqueue<SearchJob>(j => j.Run(id, CancellationToken.None));

        var withJobId = store.Get(id)!.WithHangfireJobId(hangfireJobId);
        store.Save(withJobId);
        return withJobId;
    }

    public JobSearch? Get(string id) => store.Get(id);

    public JobSearch? Active() => store.Active();

    public void Cancel(string id)
    {
        var job = store.Get(id);
        if (job is null || job.IsTerminal) return;

        if (job.HangfireJobId is not null)
            hangfire.Delete(job.HangfireJobId);

        store.Save(job.MarkCancelled(DateTimeOffset.UtcNow));
    }
}
