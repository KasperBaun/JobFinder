using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using Jobmatch.Domain;

namespace Jobmatch.Pipeline.Output;

public static class JsonReportWriter
{
    // The one writer that does not take Infrastructure.Json's shared policy. all-listings.json and
    // ranked-listings.json are artefacts the user reads, not files the app reads back, and they
    // have carried PascalCase members and PascalCase enum values since the first run. Adopting
    // camelCase here would change every existing file's shape for no reader's benefit.
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
