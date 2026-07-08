using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jobmatch.Services;

public sealed partial class MarksService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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

    // Two on-disk shapes: the original bare string ("good") and an object once a
    // reason or status exists ({ "mark": "bad", "reason": "...", "status": "applied" }).
    // Any field may be absent; an entry needs at least a valid mark or status to load.
    private static ListingMark? ParseMark(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var v = value.GetString();
            return v is "good" or "bad" ? new ListingMark(v, null) : null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var mark = ReadString(value, "mark");
            if (mark is not ("good" or "bad")) mark = null;
            var reason = ReadString(value, "reason");
            var status = ReadString(value, "status");
            if (status is not null && !ApplicationStatus.IsValid(status)) status = null;
            if (mark is null && status is null) return null;
            return new ListingMark(mark, string.IsNullOrWhiteSpace(reason) ? null : reason, status);
        }

        return null;
    }

    private static string? ReadString(JsonElement value, string property)
        => value.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private void AtomicWrite(Dictionary<string, Dictionary<string, ListingMark>> all)
    {
        var dir = Path.GetDirectoryName(ctx.MarksPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var serialisable = all.ToDictionary(
            run => run.Key,
            run => run.Value.ToDictionary(l => l.Key, l => Project(l.Value)));

        var tempPath = ctx.MarksPath + ".tmp";
        var json = JsonSerializer.Serialize(serialisable, WriteOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ctx.MarksPath, overwrite: true);
    }

    private static object Project(ListingMark mark)
    {
        if (mark is { Reason: null, Status: null } && mark.Mark is not null)
            return mark.Mark;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (mark.Mark is not null) fields["mark"] = mark.Mark;
        if (mark.Reason is not null) fields["reason"] = mark.Reason;
        if (mark.Status is not null) fields["status"] = mark.Status;
        return fields;
    }
}
