using System.Text.Encodings.Web;
using System.Text.Json;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class MarksHandler
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly object FileLock = new();

    public static IResult Set(MarkRequest? req, Jobmatch.UserContext ctx)
    {
        try
        {
            if (req is null || string.IsNullOrWhiteSpace(req.RunId) || string.IsNullOrWhiteSpace(req.ListingId))
            {
                return Results.Ok(new MarkResponse(false, "runId and listingId are required"));
            }

            var mark = req.Mark?.ToLowerInvariant();
            if (mark is not (null or "good" or "bad"))
            {
                return Results.Ok(new MarkResponse(false, "mark must be 'good', 'bad', or null"));
            }

            lock (FileLock)
            {
                var marks = LoadMarksMutable(ctx.MarksPath);

                if (mark is null)
                {
                    if (marks.TryGetValue(req.RunId, out var byListing))
                    {
                        byListing.Remove(req.ListingId);
                        if (byListing.Count == 0)
                        {
                            marks.Remove(req.RunId);
                        }
                    }
                }
                else
                {
                    if (!marks.TryGetValue(req.RunId, out var byListing))
                    {
                        byListing = new Dictionary<string, string>(StringComparer.Ordinal);
                        marks[req.RunId] = byListing;
                    }
                    byListing[req.ListingId] = mark;
                }

                AtomicWrite(ctx.MarksPath, marks);
            }

            return Results.Ok(new MarkResponse(true));
        }
        catch (Exception ex)
        {
            return Results.Ok(new MarkResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Reads marks.json into a nested dict. Returns an empty dict if the file is missing or
    /// malformed — never throws.
    /// </summary>
    public static IReadOnlyDictionary<string, Dictionary<string, string>> LoadMarks(string path)
        => LoadMarksMutable(path);

    private static Dictionary<string, Dictionary<string, string>> LoadMarksMutable(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;

        try
        {
            using var stream = File.OpenRead(path);
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

    private static void AtomicWrite(string path, Dictionary<string, Dictionary<string, string>> marks)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(marks, WriteOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }
}
