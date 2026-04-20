using System.Globalization;
using System.Text.Json;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class ManualAdapter(PortalConfig config, HttpClient http, ILogger logger, string importsDirectory)
    : BaseAdapter(config, http, logger)
{
    private readonly string _importsDirectory = importsDirectory;

    public override Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_importsDirectory))
        {
            Logger.LogWarning("portal={Portal} imports directory not found: {Dir}", PortalName, _importsDirectory);
            return Task.FromResult<IReadOnlyList<Listing>>([]);
        }

        var pattern = $"{Config.Name}-*.*";
        var files = Directory.EnumerateFiles(_importsDirectory, pattern).ToList();
        if (files.Count == 0)
        {
            Logger.LogInformation("portal={Portal} no import files matching {Pattern}", PortalName, pattern);
            return Task.FromResult<IReadOnlyList<Listing>>([]);
        }

        var listings = new List<Listing>();
        foreach (var file in files)
        {
            try
            {
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    listings.AddRange(ReadJsonFile(file));
                }
                else if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    listings.AddRange(ReadCsvFile(file));
                }
                else
                {
                    Logger.LogWarning("portal={Portal} unrecognised import file extension: {File}", PortalName, file);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} failed to read {File}", PortalName, file);
            }
        }

        return Task.FromResult<IReadOnlyList<Listing>>(listings);
    }

    private IEnumerable<Listing> ReadJsonFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) yield break;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var listing = BuildFromDict(Read(item));
            if (listing is not null) yield return listing;
        }
    }

    private IEnumerable<Listing> ReadCsvFile(string path)
    {
        using var reader = new StreamReader(path);
        var headerLine = reader.ReadLine();
        if (headerLine is null) yield break;

        var headers = CsvRow.Parse(headerLine).Select(h => h.Trim().ToLowerInvariant()).ToArray();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = CsvRow.Parse(line);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < values.Count; i++)
            {
                dict[headers[i]] = values[i];
            }
            var listing = BuildFromDict(dict);
            if (listing is not null) yield return listing;
        }
    }

    private Listing? BuildFromDict(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("url", out var urlStr) || !Uri.TryCreate(urlStr, UriKind.Absolute, out var url)) return null;
        if (!fields.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title)) return null;

        fields.TryGetValue("company", out var company);
        fields.TryGetValue("location", out var location);
        fields.TryGetValue("description", out var description);

        DateTimeOffset? postedAt = null;
        if (fields.TryGetValue("posted_at", out var postedStr) && !string.IsNullOrWhiteSpace(postedStr) &&
            DateTimeOffset.TryParse(postedStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            postedAt = parsed;
        }

        return BuildListing(
            sourceId: url.ToString(),
            title: title.Trim(),
            company: string.IsNullOrWhiteSpace(company) ? null : company.Trim(),
            location: string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            description: description ?? string.Empty,
            url: url,
            postedAt: postedAt,
            raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    private static IReadOnlyDictionary<string, string> Read(JsonElement obj)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (obj.ValueKind != JsonValueKind.Object) return dict;
        foreach (var prop in obj.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.ToString(),
                JsonValueKind.True or JsonValueKind.False => prop.Value.ToString(),
                _ => string.Empty,
            };
        }
        return dict;
    }
}

internal static class CsvRow
{
    public static IReadOnlyList<string> Parse(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else
            {
                if (ch == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (ch == '"' && sb.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
