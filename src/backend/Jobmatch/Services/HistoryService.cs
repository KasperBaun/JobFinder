using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Jobs;
using Jobmatch.Search;

namespace Jobmatch.Services;

/// <summary>
/// The "runs" view. Every run has a <see cref="JobSearch"/> lifecycle record (queued → running →
/// terminal); successful runs additionally write a rich <see cref="RunDetail"/> to the history dir.
/// This service merges both: the list is sourced from JobSearch records (so abandoned / failed /
/// running runs all appear), unioned with any legacy history files that predate the job model. Detail
/// returns the rich RunDetail when present, otherwise a lightweight one synthesised from the JobSearch.
/// </summary>
public sealed class HistoryService(UserContext ctx, IMarksService marks, IJobSearchStore jobs) : IHistoryService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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

        // Legacy runs recorded before the job model: history file with no JobSearch record.
        if (Directory.Exists(ctx.HistoryDir))
        {
            foreach (var file in Directory.EnumerateFiles(ctx.HistoryDir, "*.json"))
            {
                var detail = TryReadDetail(file);
                if (detail is null || seen.Contains(detail.RunId)) continue;
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
        }

        return summaries.OrderByDescending(r => r.StartedAt).ToList();
    }

    public RunDetail GetByRunId(string runId)
    {
        var safeId = SanitiseRunId(runId)
            ?? throw new NotFoundException($"history run '{runId}' not found");

        var job = jobs.Get(safeId);
        var path = Path.Combine(ctx.HistoryDir, $"{safeId}.json");
        var detail = File.Exists(path) ? TryReadDetail(path) : null;

        if (detail is null && job is null)
            throw new NotFoundException($"history run '{runId}' not found");

        var runMarks = marks.GetForRun(safeId);
        var goodMarks = runMarks.Values.Count(v => string.Equals(v.Mark, "good", StringComparison.OrdinalIgnoreCase));
        var marksMap = runMarks
            .Where(kvp => kvp.Value.Mark is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Mark!, StringComparer.Ordinal);
        var reasonsMap = runMarks
            .Where(kvp => kvp.Value.Reason is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Reason!, StringComparer.Ordinal);
        var statusesMap = runMarks
            .Where(kvp => kvp.Value.Status is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Status!, StringComparer.Ordinal);

        if (detail is not null)
        {
            // Rich results exist (succeeded run). Overlay lifecycle from the JobSearch when present.
            return detail with
            {
                Marks = marksMap,
                MarkReasons = reasonsMap.Count > 0 ? reasonsMap : null,
                MarkStatuses = statusesMap.Count > 0 ? statusesMap : null,
                GoodMarks = goodMarks,
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
            GoodMarks: goodMarks,
            Shortlist: [],
            Marks: marksMap,
            MarkReasons: reasonsMap.Count > 0 ? reasonsMap : null,
            MarkStatuses: statusesMap.Count > 0 ? statusesMap : null,
            State: job.State,
            Phase: job.Phase,
            Timeline: job.Timeline);
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
            var safe = SanitiseRunId(raw);
            if (safe is null)
            {
                missing.Add(raw);
                continue;
            }

            var path = Path.Combine(ctx.HistoryDir, $"{safe}.json");
            var hadHistory = File.Exists(path);
            if (hadHistory) File.Delete(path);
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

    internal static RunDetail? TryReadDetail(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<RunDetail>(stream, ReadOptions);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (IOException) when (attempt < 5) { Thread.Sleep(20); }
            catch { return null; }
        }
    }

    private static int CountGoodMarks(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> marks,
        string runId)
    {
        if (!marks.TryGetValue(runId, out var byListing)) return 0;
        return byListing.Values.Count(v => string.Equals(v.Mark, "good", StringComparison.OrdinalIgnoreCase));
    }

    private static string? SanitiseRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (runId.IndexOfAny(['/', '\\', '.', ':']) >= 0) return null;
        return runId;
    }
}
