using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Models;

namespace Jobmatch.Output;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void WriteListings(IReadOnlyCollection<Listing> listings, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, listings, Options);
    }

    public static void WriteMatches(IReadOnlyCollection<Match> matches, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, matches, Options);
    }
}
