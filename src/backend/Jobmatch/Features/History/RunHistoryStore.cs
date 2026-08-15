using System.Text.Json;
using Jobmatch.Domain.Runs;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Json;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Features.History;

public sealed class RunHistoryStore(UserContext ctx) : IRunHistoryStore
{
    // Case-insensitive on top of the shared policy: run files recorded before camelCase became the
    // convention are still on disk and must keep deserialising.
    private static readonly JsonSerializerOptions ReadOptions =
        new(JobmatchJsonOptions.Default) { PropertyNameCaseInsensitive = true };

    public void Save(RunDetail detail) =>
        AtomicFile.WriteAllText(PathFor(detail.RunId), JsonSerializer.Serialize(detail, JobmatchJsonOptions.Indented));

    public RunDetail? Find(string runId)
    {
        var safe = SanitiseRunId(runId);
        return safe is null ? null : Read(PathFor(safe));
    }

    public IReadOnlyList<RunDetail> All()
    {
        if (!Directory.Exists(ctx.HistoryDir)) return [];

        IEnumerable<string> files;
        try
        {
            // Run ids are timestamp-prefixed, so ordinal-descending filename order is newest-first.
            files = Directory.EnumerateFiles(ctx.HistoryDir, "*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return [];
        }

        return [.. files.Select(Read).OfType<RunDetail>()];
    }

    public bool Delete(string runId)
    {
        var safe = SanitiseRunId(runId);
        if (safe is null) return false;
        var path = PathFor(safe);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private string PathFor(string runId) => Path.Combine(ctx.HistoryDir, $"{runId}.json");

    /// <summary>A run id reaches this from a URL, so anything that could escape the history directory
    /// is rejected outright rather than sanitised into something else.</summary>
    internal static string? SanitiseRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (runId.IndexOfAny(['/', '\\', '.', ':']) >= 0) return null;
        return runId;
    }

    // A search run may be writing this very file, and a virus scanner or file-sync agent can hold it
    // open for a moment, so share the handle widely and retry a transient IO failure before giving up.
    private static RunDetail? Read(string path)
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
}
