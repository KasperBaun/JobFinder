using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Search;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class HistoryHandler
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IResult List(Jobmatch.UserContext ctx)
    {
        try
        {
            var marks = MarksHandler.LoadMarks(ctx.MarksPath);
            var runs = new List<RunSummary>();

            if (Directory.Exists(ctx.HistoryDir))
            {
                foreach (var file in Directory.EnumerateFiles(ctx.HistoryDir, "*.json"))
                {
                    var detail = TryReadDetail(file);
                    if (detail is null) continue;
                    var goodMarks = CountGoodMarks(marks, detail.RunId);
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

            var ordered = runs
                .OrderByDescending(r => r.StartedAt)
                .ToList();

            return Results.Ok(new Models.HistoryResponse(ordered));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static IResult Detail(string runId, Jobmatch.UserContext ctx)
    {
        try
        {
            var safeId = SanitiseRunId(runId);
            if (safeId is null) return Results.NotFound();

            var path = Path.Combine(ctx.HistoryDir, $"{safeId}.json");
            if (!File.Exists(path)) return Results.NotFound();

            var detail = TryReadDetail(path);
            if (detail is null) return Results.NotFound();

            var marks = MarksHandler.LoadMarks(ctx.MarksPath);
            var runMarks = marks.TryGetValue(detail.RunId, out var m)
                ? new Dictionary<string, string>(m, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            var goodMarks = runMarks.Values.Count(v => string.Equals(v, "good", StringComparison.OrdinalIgnoreCase));

            var withMarks = detail with
            {
                Marks = runMarks,
                GoodMarks = goodMarks,
            };

            return Results.Ok(withMarks);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
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

    private static int CountGoodMarks(IReadOnlyDictionary<string, Dictionary<string, string>> marks, string runId)
    {
        if (!marks.TryGetValue(runId, out var byListing)) return 0;
        return byListing.Values.Count(v => string.Equals(v, "good", StringComparison.OrdinalIgnoreCase));
    }

    private static string? SanitiseRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        // RunIds are constructed from a UTC stamp + 6-hex suffix; reject anything with path separators.
        if (runId.IndexOfAny(['/', '\\', '.', ':']) >= 0) return null;
        return runId;
    }
}
