using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Jobmatch.Platform.IO;
using Jobmatch.Platform.Json;

namespace Jobmatch.Features.Applications;

public sealed partial class MarksService
{
    private static readonly JsonSerializerOptions WriteOptions = JobmatchJsonOptions.Indented;

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

    // Two on-disk shapes: the original bare string ("good") and an object once any
    // extra exists ({ "mark": "bad", "reason": "...", "status": "applied", "statusAt": "..." }).
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
            var statusAt = status is null ? null : ReadTimestamp(value, "statusAt");
            return new ListingMark(mark, string.IsNullOrWhiteSpace(reason) ? null : reason, status, statusAt);
        }

        return null;
    }

    private static string? ReadString(JsonElement value, string property)
        => value.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement value, string property)
        => value.TryGetProperty(property, out var el)
            && el.ValueKind == JsonValueKind.String
            && el.TryGetDateTimeOffset(out var at)
                ? at
                : null;

    private void AtomicWrite(Dictionary<string, Dictionary<string, ListingMark>> all)
    {
        var serialisable = all.ToDictionary(
            run => run.Key,
            run => run.Value.ToDictionary(l => l.Key, l => Project(l.Value)));

        AtomicFile.WriteAllText(ctx.MarksPath, JsonSerializer.Serialize(serialisable, WriteOptions));
    }

    private static object Project(ListingMark mark)
    {
        if (mark is { Reason: null, Status: null, StatusChangedAt: null } && mark.Mark is not null)
            return mark.Mark;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (mark.Mark is not null) fields["mark"] = mark.Mark;
        if (mark.Reason is not null) fields["reason"] = mark.Reason;
        if (mark.Status is not null) fields["status"] = mark.Status;
        if (mark.StatusChangedAt is not null)
            fields["statusAt"] = mark.StatusChangedAt.Value.ToString("O", CultureInfo.InvariantCulture);
        return fields;
    }
}
