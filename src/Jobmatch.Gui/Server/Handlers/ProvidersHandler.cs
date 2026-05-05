using System.Text.Json;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Jobmatch.Models;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class ProvidersHandler
{
    public static IResult Get(Jobmatch.UserContext ctx)
    {
        try
        {
            var portals = PortalConfigLoader.Load(ctx.PortalsPath);
            var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);

            var summaries = portals.Select(p =>
            {
                lastByProvider.TryGetValue(p.Name, out var last);
                return new ProviderSummary(
                    Name: p.Name,
                    Type: p.Type.ToString().ToLowerInvariant(),
                    Enabled: p.Enabled,
                    BaseUrl: p.BaseUrl?.ToString(),
                    Endpoint: p.Endpoint?.ToString(),
                    RateLimitRps: p.RateLimitRps,
                    Notes: p.Notes,
                    LastFetchedAt: last?.FetchedAt,
                    LastFetchCount: last?.FetchedCount);
            }).ToList();

            return Results.Ok(new ProvidersResponse(summaries));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private sealed record LastFetch(DateTimeOffset FetchedAt, int? FetchedCount);

    /// <summary>
    /// Walks the history directory newest-first and returns the most recent per-provider entry
    /// across all runs. Tolerates malformed/missing files — never throws.
    /// </summary>
    private static Dictionary<string, LastFetch> LoadLastFetchByProvider(string historyDir)
    {
        var result = new Dictionary<string, LastFetch>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(historyDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(historyDir, "*.json")
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
                // Skip malformed history files; they shouldn't break the providers list.
            }
        }

        return result;
    }
}
