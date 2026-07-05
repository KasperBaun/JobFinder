using System.Text.Json;

namespace Jobmatch.Services;

// Projects each provider's most-recent-run info out of the per-user history/*.json files. Kept in
// its own partial so the core service file stays under the size limit.
public sealed partial class ProvidersService
{
    private Dictionary<string, LastFetch> LoadLastFetchByProvider()
    {
        var result = new Dictionary<string, LastFetch>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(ctx.HistoryDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(ctx.HistoryDir, "*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal);
        }
        catch
        {
            return result;
        }

        foreach (var file in files)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                if (!doc.RootElement.TryGetProperty("startedAt", out var startedProp)) continue;
                if (!doc.RootElement.TryGetProperty("providers", out var providersProp)) continue;
                if (providersProp.ValueKind != JsonValueKind.Array) continue;
                if (!startedProp.TryGetDateTimeOffset(out var startedAt)) continue;

                foreach (var prov in providersProp.EnumerateArray())
                {
                    if (prov.ValueKind != JsonValueKind.Object) continue;
                    if (!prov.TryGetProperty("name", out var nameProp)) continue;
                    var name = nameProp.GetString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    int? count = null;
                    if (prov.TryGetProperty("fetchedCount", out var countProp) &&
                        countProp.ValueKind == JsonValueKind.Number)
                    {
                        count = countProp.GetInt32();
                    }

                    if (!result.ContainsKey(name))
                    {
                        result[name] = new LastFetch(startedAt, count);
                    }
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private IReadOnlyList<ProviderRunHistory> LoadRecentRuns(string providerName, int take)
    {
        var result = new List<ProviderRunHistory>();
        if (!Directory.Exists(ctx.HistoryDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(ctx.HistoryDir, "*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal);
        }
        catch
        {
            return result;
        }

        foreach (var file in files)
        {
            if (result.Count >= take) break;
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                if (!doc.RootElement.TryGetProperty("runId", out var runIdProp)) continue;
                if (!doc.RootElement.TryGetProperty("startedAt", out var startedProp)) continue;
                if (!doc.RootElement.TryGetProperty("providers", out var providersProp)) continue;
                if (providersProp.ValueKind != JsonValueKind.Array) continue;
                if (!startedProp.TryGetDateTimeOffset(out var startedAt)) continue;

                foreach (var prov in providersProp.EnumerateArray())
                {
                    if (prov.ValueKind != JsonValueKind.Object) continue;
                    if (!prov.TryGetProperty("name", out var nameProp)) continue;
                    if (!string.Equals(nameProp.GetString(), providerName, StringComparison.OrdinalIgnoreCase)) continue;

                    var status = prov.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "unknown" : "unknown";
                    int? count = prov.TryGetProperty("fetchedCount", out var cProp) && cProp.ValueKind == JsonValueKind.Number
                        ? cProp.GetInt32()
                        : null;
                    string? error = prov.TryGetProperty("error", out var eProp) && eProp.ValueKind == JsonValueKind.String
                        ? eProp.GetString()
                        : null;

                    result.Add(new ProviderRunHistory(
                        RunId: runIdProp.GetString() ?? "",
                        StartedAt: startedAt,
                        Status: status,
                        FetchedCount: count,
                        Error: error));
                    break;
                }
            }
            catch
            {
            }
        }

        return result;
    }
}
