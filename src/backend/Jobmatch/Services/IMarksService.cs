using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jobmatch.Services;

// A mark is "good" or "bad", optionally annotated with a short free-form reason
// ("I'm not a student") that the LLM judge consumes as few-shot signal on later runs.
public sealed record ListingMark(string Mark, string? Reason);

public interface IMarksService
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> LoadAll();
    IReadOnlyDictionary<string, ListingMark> GetForRun(string runId);
    void Set(string runId, string listingId, string? mark, string? reason);
    void RemoveRuns(IEnumerable<string> runIds);
}

public sealed class MarksService(UserContext ctx) : IMarksService
{
    private const int MaxReasonLength = 500;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _fileLock = new();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> LoadAll()
    {
        var raw = LoadMutable();
        return raw.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, ListingMark>)kvp.Value);
    }

    public IReadOnlyDictionary<string, ListingMark> GetForRun(string runId)
    {
        var all = LoadMutable();
        if (all.TryGetValue(runId, out var byListing))
        {
            return new Dictionary<string, ListingMark>(byListing, StringComparer.Ordinal);
        }
        return new Dictionary<string, ListingMark>(StringComparer.Ordinal);
    }

    public void Set(string runId, string listingId, string? mark, string? reason)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new InvalidRequestException("runId is required");
        if (string.IsNullOrWhiteSpace(listingId)) throw new InvalidRequestException("listingId is required");

        var normalised = mark?.ToLowerInvariant();
        if (normalised is not (null or "good" or "bad"))
            throw new InvalidRequestException("mark must be 'good', 'bad', or null");

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > MaxReasonLength })
            throw new InvalidRequestException($"reason must be {MaxReasonLength} characters or fewer");

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
                    byListing = new Dictionary<string, ListingMark>(StringComparer.Ordinal);
                    all[runId] = byListing;
                }
                byListing[listingId] = new ListingMark(normalised, trimmedReason);
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

    private Dictionary<string, Dictionary<string, ListingMark>> LoadMutable()
    {
        var result = new Dictionary<string, Dictionary<string, ListingMark>>(StringComparer.Ordinal);
        if (!File.Exists(ctx.MarksPath)) return result;

        try
        {
            using var stream = File.OpenRead(ctx.MarksPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var runProp in doc.RootElement.EnumerateObject())
            {
                if (runProp.Value.ValueKind != JsonValueKind.Object) continue;
                var byListing = new Dictionary<string, ListingMark>(StringComparer.Ordinal);
                foreach (var listingProp in runProp.Value.EnumerateObject())
                {
                    var mark = ParseMark(listingProp.Value);
                    if (mark is not null) byListing[listingProp.Name] = mark;
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

    // Two on-disk shapes: the original bare string ("good") and, once a reason
    // exists, an object ({ "mark": "bad", "reason": "..." }).
    private static ListingMark? ParseMark(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var v = value.GetString();
            return v is "good" or "bad" ? new ListingMark(v, null) : null;
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("mark", out var markEl)
            && markEl.ValueKind == JsonValueKind.String)
        {
            var v = markEl.GetString();
            if (v is not ("good" or "bad")) return null;
            var reason = value.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString()
                : null;
            return new ListingMark(v, string.IsNullOrWhiteSpace(reason) ? null : reason);
        }

        return null;
    }

    private void AtomicWrite(Dictionary<string, Dictionary<string, ListingMark>> all)
    {
        var dir = Path.GetDirectoryName(ctx.MarksPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var serialisable = all.ToDictionary(
            run => run.Key,
            run => run.Value.ToDictionary(
                l => l.Key,
                l => l.Value.Reason is null ? (object)l.Value.Mark : new { mark = l.Value.Mark, reason = l.Value.Reason }));

        var tempPath = ctx.MarksPath + ".tmp";
        var json = JsonSerializer.Serialize(serialisable, WriteOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ctx.MarksPath, overwrite: true);
    }
}
