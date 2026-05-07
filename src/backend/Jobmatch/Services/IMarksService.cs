using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jobmatch.Services;

public interface IMarksService
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadAll();
    IReadOnlyDictionary<string, string> GetForRun(string runId);
    void Set(string runId, string listingId, string? mark);
    void RemoveRuns(IEnumerable<string> runIds);
}

public sealed class MarksService(UserContext ctx) : IMarksService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _fileLock = new();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
    {
        var raw = LoadMutable();
        return raw.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, string>)kvp.Value);
    }

    public IReadOnlyDictionary<string, string> GetForRun(string runId)
    {
        var all = LoadMutable();
        if (all.TryGetValue(runId, out var byListing))
        {
            return new Dictionary<string, string>(byListing, StringComparer.Ordinal);
        }
        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public void Set(string runId, string listingId, string? mark)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new InvalidRequestException("runId is required");
        if (string.IsNullOrWhiteSpace(listingId)) throw new InvalidRequestException("listingId is required");

        var normalised = mark?.ToLowerInvariant();
        if (normalised is not (null or "good" or "bad"))
            throw new InvalidRequestException("mark must be 'good', 'bad', or null");

        lock (_fileLock)
        {
            var all = LoadMutable();

            if (normalised is null)
            {
                if (all.TryGetValue(runId, out var byListing))
                {
                    byListing.Remove(listingId);
                    if (byListing.Count == 0) all.Remove(runId);
                }
            }
            else
            {
                if (!all.TryGetValue(runId, out var byListing))
                {
                    byListing = new Dictionary<string, string>(StringComparer.Ordinal);
                    all[runId] = byListing;
                }
                byListing[listingId] = normalised;
            }

            AtomicWrite(all);
        }
    }

    public void RemoveRuns(IEnumerable<string> runIds)
    {
        lock (_fileLock)
        {
            var all = LoadMutable();
            var changed = false;
            foreach (var id in runIds)
            {
                if (all.Remove(id)) changed = true;
            }
            if (changed) AtomicWrite(all);
        }
    }

    private Dictionary<string, Dictionary<string, string>> LoadMutable()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (!File.Exists(ctx.MarksPath)) return result;

        try
        {
            using var stream = File.OpenRead(ctx.MarksPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var runProp in doc.RootElement.EnumerateObject())
            {
                if (runProp.Value.ValueKind != JsonValueKind.Object) continue;
                var byListing = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var listingProp in runProp.Value.EnumerateObject())
                {
                    if (listingProp.Value.ValueKind != JsonValueKind.String) continue;
                    var v = listingProp.Value.GetString();
                    if (v is "good" or "bad")
                    {
                        byListing[listingProp.Name] = v;
                    }
                }
                if (byListing.Count > 0)
                {
                    result[runProp.Name] = byListing;
                }
            }
        }
        catch
        {
            // Treat unreadable marks as no marks. The next write will replace the file.
        }

        return result;
    }

    private void AtomicWrite(Dictionary<string, Dictionary<string, string>> all)
    {
        var dir = Path.GetDirectoryName(ctx.MarksPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = ctx.MarksPath + ".tmp";
        var json = JsonSerializer.Serialize(all, WriteOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ctx.MarksPath, overwrite: true);
    }
}
