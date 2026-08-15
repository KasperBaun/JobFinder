using Jobmatch.Domain.Runs;
using Jobmatch.Features.Applications;
using Jobmatch.Search;

namespace Jobmatch.Features.History;

/// <summary>
/// The "runs" view. Every run has a <see cref="JobSearch"/> lifecycle record (queued → running →
/// terminal); successful runs additionally write a rich <see cref="RunDetail"/> to the history dir.
/// This service merges both: the list is sourced from JobSearch records (so abandoned / failed /
/// running runs all appear), unioned with any legacy history files that predate the job model. Detail
/// returns the rich RunDetail when present, otherwise a lightweight one synthesised from the JobSearch.
/// </summary>
public sealed class HistoryService(IRunHistoryStore runs, IMarksService marks, IJobSearchStore jobs) : IHistoryService
{
    public IReadOnlyList<RunSummary> List()
    {
        var allMarks = marks.LoadAll();
        var summaries = new List<RunSummary>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var job in jobs.List())
        {
            seen.Add(job.Id);
            summaries.Add(new RunSummary(
                RunId: job.Id,
                StartedAt: job.StartedAt ?? job.CreatedAt,
                Providers: job.Providers,
                FetchedCount: job.FetchedCount,
                DedupedCount: job.DedupedCount,
                RankedCount: job.RankedCount,
                ShortlistCount: job.ShortlistCount,
                TopScore: job.TopScore,
                GoodMarks: CountGoodMarks(allMarks, job.Id),
                State: job.State,
                Phase: job.Phase));
        }

        // Legacy runs recorded before the job model: a history file with no JobSearch record.
        foreach (var detail in runs.All())
        {
            if (seen.Contains(detail.RunId)) continue;
            summaries.Add(new RunSummary(
                RunId: detail.RunId,
                StartedAt: detail.StartedAt,
                Providers: detail.Providers,
                FetchedCount: detail.FetchedCount,
                DedupedCount: detail.DedupedCount,
                RankedCount: detail.RankedCount,
                ShortlistCount: detail.ShortlistCount,
                TopScore: detail.TopScore,
                GoodMarks: CountGoodMarks(allMarks, detail.RunId),
                State: JobSearchState.Succeeded,
                Phase: JobSearchPhase.Done));
        }

        return summaries.OrderByDescending(r => r.StartedAt).ToList();
    }

    public RunDetail GetByRunId(string runId)
    {
        var safeId = RunHistoryStore.SanitiseRunId(runId)
            ?? throw new NotFoundException($"history run '{runId}' not found");

        var job = jobs.Get(safeId);
        var detail = runs.Find(safeId);

        if (detail is null && job is null)
            throw new NotFoundException($"history run '{runId}' not found");

        var maps = BuildMarkMaps(marks.GetForRun(safeId));

        if (detail is not null)
        {
            // Rich results exist (succeeded run). Overlay lifecycle from the JobSearch when present.
            return detail with
            {
                Marks = maps.Marks,
                MarkReasons = maps.Reasons,
                MarkStatuses = maps.Statuses,
                MarkStatusAt = maps.StatusAt,
                GoodMarks = maps.GoodMarks,
                State = job?.State ?? JobSearchState.Succeeded,
                Phase = job?.Phase ?? JobSearchPhase.Done,
                Timeline = job?.Timeline ?? detail.Timeline,
            };
        }

        // No results yet (queued / running / failed / cancelled / interrupted): synthesise from the job.
        return new RunDetail(
            RunId: job!.Id,
            StartedAt: job.StartedAt ?? job.CreatedAt,
            Providers: job.Providers,
            FetchedCount: job.FetchedCount,
            DedupedCount: job.DedupedCount,
            RankedCount: job.RankedCount,
            ShortlistCount: job.ShortlistCount,
            TopScore: job.TopScore,
            GoodMarks: maps.GoodMarks,
            Shortlist: [],
            Marks: maps.Marks,
            MarkReasons: maps.Reasons,
            MarkStatuses: maps.Statuses,
            MarkStatusAt: maps.StatusAt,
            State: job.State,
            Phase: job.Phase,
            Timeline: job.Timeline);
    }

    private sealed record MarkMaps(
        IReadOnlyDictionary<string, string> Marks,
        IReadOnlyDictionary<string, string>? Reasons,
        IReadOnlyDictionary<string, string>? Statuses,
        IReadOnlyDictionary<string, DateTimeOffset>? StatusAt,
        int GoodMarks);

    private static MarkMaps BuildMarkMaps(IReadOnlyDictionary<string, ListingMark> runMarks)
    {
        var marksMap = runMarks
            .Where(kvp => kvp.Value.Mark is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Mark!, StringComparer.Ordinal);
        var reasonsMap = runMarks
            .Where(kvp => kvp.Value.Reason is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Reason!, StringComparer.Ordinal);
        var statusesMap = runMarks
            .Where(kvp => kvp.Value.Status is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Status!, StringComparer.Ordinal);
        var statusAtMap = runMarks
            .Where(kvp => kvp.Value.StatusChangedAt is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StatusChangedAt!.Value, StringComparer.Ordinal);

        return new MarkMaps(
            marksMap,
            reasonsMap.Count > 0 ? reasonsMap : null,
            statusesMap.Count > 0 ? statusesMap : null,
            statusAtMap.Count > 0 ? statusAtMap : null,
            runMarks.Values.Count(v => string.Equals(v.Mark, "good", StringComparison.OrdinalIgnoreCase)));
    }

    public HistoryDeleteResult Delete(IReadOnlyList<string> runIds)
    {
        if (runIds.Count == 0)
            throw new InvalidRequestException("runIds is required");

        var deleted = 0;
        var missing = new List<string>();
        var pruned = new List<string>();

        foreach (var raw in runIds)
        {
            var safe = RunHistoryStore.SanitiseRunId(raw);
            if (safe is null)
            {
                missing.Add(raw);
                continue;
            }

            var hadHistory = runs.Delete(safe);
            var removedJob = jobs.Delete([safe]) > 0;

            if (hadHistory || removedJob)
            {
                deleted++;
                pruned.Add(safe);
            }
            else
            {
                missing.Add(safe);
            }
        }

        if (pruned.Count > 0)
            marks.RemoveRuns(pruned);

        return new HistoryDeleteResult(deleted, missing);
    }

    private static int CountGoodMarks(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> marks,
        string runId)
    {
        if (!marks.TryGetValue(runId, out var byListing)) return 0;
        return byListing.Values.Count(v => string.Equals(v.Mark, "good", StringComparison.OrdinalIgnoreCase));
    }
}
