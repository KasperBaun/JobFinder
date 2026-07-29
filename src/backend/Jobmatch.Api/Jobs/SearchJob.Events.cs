using Jobmatch.Jobs;
using Jobmatch.Search;

namespace Jobmatch.Api.Jobs;

/// <summary>
/// Projects the pipeline's typed <see cref="SearchProgressEvent"/>s onto the run's timeline.
/// <para>
/// Each entry carries both the English prose and a stable key + args. The prose is what logs and
/// pre-existing runs show; the key is what lets the GUI render the timeline in the user's language,
/// including for runs already persisted under <c>data/&lt;email&gt;/jobsearch/</c>. Keys are a
/// contract — additive only.
/// </para>
/// </summary>
public sealed partial class SearchJob
{
    private JobSearch Apply(JobSearch job, SearchProgressEvent evt)
    {
        var now = DateTimeOffset.UtcNow;
        switch (evt)
        {
            case StartedEvent s:
                return job.Log(
                    JobSearchEventLevel.Info, JobSearchPhase.Fetching,
                    $"Fetching listings from {s.Total} sources", now,
                    messageKey: "fetchingFrom", args: new Dictionary<string, object> { ["total"] = s.Total });

            case ProviderRunningEvent:
            case ProviderDoneEvent:
            case ProviderFailedEvent:
                return ApplyProvider(job, evt, now);

            case DedupeEvent d:
                return job
                    .WithCounts(deduped: d.MergedCount)
                    .Log(
                        JobSearchEventLevel.Info, JobSearchPhase.Deduping,
                        $"{d.MergedCount} unique jobs after removing duplicates", now, count: d.MergedCount,
                        messageKey: "deduped", args: new Dictionary<string, object> { ["count"] = d.MergedCount });

            case LlmJudgingEvent l:
                return job.Log(
                    JobSearchEventLevel.Info, JobSearchPhase.LlmJudging,
                    $"AI reviewing top {l.Total} jobs…", now, count: l.Total,
                    messageKey: "llmJudging", args: new Dictionary<string, object> { ["count"] = l.Total });

            case RankEvent r:
                return job
                    .WithCounts(ranked: r.RankedCount, shortlist: r.RankedCount, topScore: r.TopScore)
                    .Log(
                        JobSearchEventLevel.Info, JobSearchPhase.Ranking,
                        $"{r.RankedCount} jobs rated · best {r.TopScore:0.00}", now, count: r.RankedCount,
                        messageKey: "ranked",
                        args: new Dictionary<string, object> { ["count"] = r.RankedCount, ["topScore"] = r.TopScore });

            case CompleteEvent c:
                return job
                    .WithCounts(shortlist: c.Shortlist.Count, topScore: c.Shortlist.Count > 0 ? c.Shortlist[0].Score : job.TopScore)
                    .Log(
                        JobSearchEventLevel.Info, JobSearchPhase.Writing,
                        "Writing results", now, count: c.Shortlist.Count,
                        messageKey: "writing");

            case ErrorEvent e:
                throw new InvalidOperationException(e.Message);

            default:
                return job;
        }
    }

    private static JobSearch ApplyProvider(JobSearch job, SearchProgressEvent evt, DateTimeOffset now) => evt switch
    {
        ProviderRunningEvent p => FromProviderRunning(job, p, now),
        ProviderDoneEvent p => FromProviderDone(job, p, now),
        ProviderFailedEvent p => FromProviderFailed(job, p, now),
        _ => job,
    };

    private static JobSearch FromProviderRunning(JobSearch job, ProviderRunningEvent p, DateTimeOffset now) => job
        .WithProviders(Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Running, null, null)))
        .Log(
            JobSearchEventLevel.Info, JobSearchPhase.Fetching,
            $"Fetching {p.Provider} ({p.Index}/{p.Total})", now, p.Provider,
            messageKey: "providerRunning",
            args: new Dictionary<string, object> { ["provider"] = p.Provider, ["index"] = p.Index, ["total"] = p.Total });

    private static JobSearch FromProviderDone(JobSearch job, ProviderDoneEvent p, DateTimeOffset now)
    {
        var providers = Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Ok, p.FetchedCount, null, p.DurationMs, p.HitPageCap, p.PossiblyCapped));
        var withCounts = job.WithProviders(providers).WithCounts(fetched: SumOk(providers));
        var capNote = p.HitPageCap ? " · hit page cap, may be more"
            : p.PossiblyCapped ? " · at configured limit, may be more"
            : "";
        var level = p.HitPageCap || p.PossiblyCapped ? JobSearchEventLevel.Warn : JobSearchEventLevel.Info;

        // Three keys rather than an appended clause — Danish word order will not tolerate a suffix.
        var key = p.HitPageCap ? "providerDoneCapped"
            : p.PossiblyCapped ? "providerDoneLimited"
            : "providerDone";

        return withCounts.Log(
            level, JobSearchPhase.Fetching,
            $"{p.Provider}: {p.FetchedCount} jobs · {p.DurationMs / 1000.0:0}s{capNote}", now,
            p.Provider, p.FetchedCount, p.DurationMs,
            messageKey: key,
            args: new Dictionary<string, object>
            {
                ["provider"] = p.Provider,
                ["count"] = p.FetchedCount,
                ["seconds"] = p.DurationMs / 1000.0,
            });
    }

    private static JobSearch FromProviderFailed(JobSearch job, ProviderFailedEvent p, DateTimeOffset now) => job
        .WithProviders(Upsert(job.Providers, new ProviderRunStatus(p.Provider, ProviderRunState.Failed, null, p.Error, p.DurationMs)))
        .Log(
            JobSearchEventLevel.Warn, JobSearchPhase.Fetching,
            $"{p.Provider} failed: {p.Error} · {p.DurationMs / 1000.0:0}s", now, p.Provider, durationMs: p.DurationMs,
            messageKey: "providerFailed",
            args: new Dictionary<string, object>
            {
                ["provider"] = p.Provider,
                ["error"] = p.Error,
                ["seconds"] = p.DurationMs / 1000.0,
            });
}
