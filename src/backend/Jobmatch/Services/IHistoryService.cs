using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Search;

namespace Jobmatch.Services;

public sealed record HistoryDeleteResult(int Deleted, IReadOnlyList<string> Missing);

public interface IHistoryService
{
    IReadOnlyList<RunSummary> List();
    RunDetail GetByRunId(string runId);
    HistoryDeleteResult Delete(IReadOnlyList<string> runIds);
}

public sealed class HistoryService(UserContext ctx, IMarksService marks) : IHistoryService
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
        var runs = new List<RunSummary>();

        if (Directory.Exists(ctx.HistoryDir))
        {
            foreach (var file in Directory.EnumerateFiles(ctx.HistoryDir, "*.json"))
            {
                var detail = TryReadDetail(file);
                if (detail is null) continue;
                var goodMarks = CountGoodMarks(allMarks, detail.RunId);
                runs.Add(new RunSummary(
                    RunId: detail.RunId,
                    StartedAt: detail.StartedAt,
                    Providers: detail.Providers,
                    FetchedCount: detail.FetchedCount,
                    DedupedCount: detail.DedupedCount,
                    RankedCount: detail.RankedCount,
                    ShortlistCount: detail.ShortlistCount,
                    TopScore: detail.TopScore,
                    GoodMarks: goodMarks));
            }
        }

        return runs.OrderByDescending(r => r.StartedAt).ToList();
    }

    public RunDetail GetByRunId(string runId)
    {
        var safeId = SanitiseRunId(runId)
            ?? throw new NotFoundException($"history run '{runId}' not found");

        var path = Path.Combine(ctx.HistoryDir, $"{safeId}.json");
        if (!File.Exists(path))
            throw new NotFoundException($"history run '{runId}' not found");

        var detail = TryReadDetail(path)
            ?? throw new NotFoundException($"history run '{runId}' not found");

        var runMarks = marks.GetForRun(detail.RunId);
        var goodMarks = runMarks.Values.Count(v => string.Equals(v, "good", StringComparison.OrdinalIgnoreCase));

        return detail with
        {
            Marks = new Dictionary<string, string>(runMarks, StringComparer.Ordinal),
            GoodMarks = goodMarks,
        };
    }

    public HistoryDeleteResult Delete(IReadOnlyList<string> runIds)
    {
        if (runIds.Count == 0)
            throw new InvalidRequestException("runIds is required");

        var deleted = 0;
        var missing = new List<string>();
        var prunedFromMarks = new List<string>();

        foreach (var raw in runIds)
        {
            var safe = SanitiseRunId(raw);
            if (safe is null)
            {
                missing.Add(raw);
                continue;
            }

            var path = Path.Combine(ctx.HistoryDir, $"{safe}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                deleted++;
                prunedFromMarks.Add(safe);
            }
            else
            {
                missing.Add(safe);
            }
        }

        if (prunedFromMarks.Count > 0)
        {
            marks.RemoveRuns(prunedFromMarks);
        }

        return new HistoryDeleteResult(deleted, missing);
    }

    internal static RunDetail? TryReadDetail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<RunDetail>(stream, ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static int CountGoodMarks(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> marks,
        string runId)
    {
        if (!marks.TryGetValue(runId, out var byListing)) return 0;
        return byListing.Values.Count(v => string.Equals(v, "good", StringComparison.OrdinalIgnoreCase));
    }

    private static string? SanitiseRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (runId.IndexOfAny(['/', '\\', '.', ':']) >= 0) return null;
        return runId;
    }
}
