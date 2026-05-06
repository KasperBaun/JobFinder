using System.Diagnostics;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Jobmatch.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Serialization;

namespace Jobmatch.Gui.Server.Handlers;

public static class ProvidersHandler
{
    private static readonly object FileLock = new();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();
    private static readonly ISerializer Serializer = new SerializerBuilder().Build();

    public static IResult GetList(Jobmatch.UserContext ctx)
    {
        try
        {
            var portals = PortalConfigLoader.Load(ctx.PortalsPath);
            var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);

            var summaries = portals.Select(p => MakeSummary(p, lastByProvider)).ToList();
            return Results.Ok(new ProvidersResponse(summaries));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static IResult GetOne(int id, Jobmatch.UserContext ctx)
    {
        try
        {
            var portals = PortalConfigLoader.Load(ctx.PortalsPath);
            var match = portals.FirstOrDefault(p => p.Id == id);
            if (match is null) return Results.NotFound();

            var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);
            lastByProvider.TryGetValue(match.Name, out var last);
            var recent = LoadRecentRuns(ctx.HistoryDir, match.Name, take: 5);

            return Results.Ok(new ProviderDetail(
                Id: match.Id,
                Name: match.Name,
                Type: match.Type.ToString().ToLowerInvariant(),
                Enabled: match.Enabled,
                Endpoint: match.Endpoint?.ToString(),
                RateLimitRps: match.RateLimitRps,
                Notes: match.Notes,
                LastFetchedAt: last?.FetchedAt,
                LastFetchCount: last?.FetchedCount,
                RecentRuns: recent));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static IResult Create(ProviderUpsert? req, Jobmatch.UserContext ctx)
    {
        if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

        try
        {
            int newId = 0;
            SaveMutated(ctx, entries =>
            {
                var existingIds = entries.OfType<IDictionary<object, object?>>()
                    .Select(d => d.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : 0)
                    .DefaultIfEmpty(0)
                    .Max();
                newId = existingIds + 1;

                var node = new Dictionary<object, object?>();
                ApplyUpsert(node, req, newId);
                entries.Add(node);
            });

            return Results.Ok(new CreateResponse(true, newId));
        }
        catch (ConfigException ex)
        {
            return Results.Ok(new CreateResponse(false, 0, ex.Message));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"POST /api/providers — {ex.GetType().Name}: {ex.Message}");
            return Results.Ok(new CreateResponse(false, 0, ex.Message));
        }
    }

    public static IResult Update(int id, ProviderUpsert? req, Jobmatch.UserContext ctx)
    {
        if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

        try
        {
            var found = false;
            SaveMutated(ctx, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i] is not IDictionary<object, object?> map) continue;
                    if (!TryGetId(map, out var entryId) || entryId != id) continue;

                    ApplyUpsert(map, req, id);
                    found = true;
                    return;
                }
            });

            if (!found) return Results.NotFound();
            return Results.Ok(new SaveResponse(true));
        }
        catch (ConfigException ex)
        {
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"PUT /api/providers/{id} — {ex.GetType().Name}: {ex.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    public static IResult Delete(int id, Jobmatch.UserContext ctx)
    {
        try
        {
            var removed = false;
            SaveMutated(ctx, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i] is not IDictionary<object, object?> map) continue;
                    if (!TryGetId(map, out var entryId) || entryId != id) continue;
                    entries.RemoveAt(i);
                    removed = true;
                    return;
                }
            });

            if (!removed) return Results.NotFound();
            return Results.Ok(new SaveResponse(true));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"DELETE /api/providers/{id} — {ex.GetType().Name}: {ex.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    public static async Task<IResult> Test(int id, Jobmatch.UserContext ctx, CancellationToken ct)
    {
        try
        {
            var portals = PortalConfigLoader.Load(ctx.PortalsPath);
            var portal = portals.FirstOrDefault(p => p.Id == id);
            if (portal is null) return Results.NotFound();

            if (portal.Type == PortalType.Manual)
            {
                return Results.Ok(new ProviderTestResult(
                    Ok: false, FetchedCount: 0, DurationMs: 0,
                    SampleTitle: null, Error: "manual provider — no live endpoint to test",
                    TestedAt: DateTimeOffset.UtcNow));
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var adapter = AdapterFactory.Create(portal, http, NullLogger.Instance, ctx.ImportsDir);
            if (adapter is null)
            {
                return Results.Ok(new ProviderTestResult(
                    Ok: false, FetchedCount: 0, DurationMs: 0,
                    SampleTitle: null, Error: $"unsupported portal type '{portal.Type}'",
                    TestedAt: DateTimeOffset.UtcNow));
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var results = await adapter.FetchAsync(ct).ConfigureAwait(false);
                sw.Stop();
                var sample = results.Count > 0 ? results[0].Title : null;
                return Results.Ok(new ProviderTestResult(
                    Ok: results.Count > 0,
                    FetchedCount: results.Count,
                    DurationMs: sw.ElapsedMilliseconds,
                    SampleTitle: sample,
                    Error: results.Count == 0 ? "fetch returned 0 listings" : null,
                    TestedAt: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Results.Ok(new ProviderTestResult(
                    Ok: false,
                    FetchedCount: 0,
                    DurationMs: sw.ElapsedMilliseconds,
                    SampleTitle: null,
                    Error: ex.Message,
                    TestedAt: DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static ProviderSummary MakeSummary(PortalConfig p, IReadOnlyDictionary<string, LastFetch> lastByProvider)
    {
        lastByProvider.TryGetValue(p.Name, out var last);
        return new ProviderSummary(
            Id: p.Id,
            Name: p.Name,
            Type: p.Type.ToString().ToLowerInvariant(),
            Enabled: p.Enabled,
            Endpoint: p.Endpoint?.ToString(),
            RateLimitRps: p.RateLimitRps,
            Notes: p.Notes,
            LastFetchedAt: last?.FetchedAt,
            LastFetchCount: last?.FetchedCount);
    }

    private static void SaveMutated(Jobmatch.UserContext ctx, Action<List<object?>> mutate)
    {
        lock (FileLock)
        {
            var yaml = File.Exists(ctx.PortalsPath)
                ? File.ReadAllText(ctx.PortalsPath)
                : "portals: []\n";

            Dictionary<object, object?>? root = null;
            if (!string.IsNullOrWhiteSpace(yaml))
            {
                try { root = Deserializer.Deserialize<Dictionary<object, object?>>(yaml); }
                catch { root = null; }
            }
            root ??= new Dictionary<object, object?>();

            if (!root.TryGetValue("portals", out var entriesObj) || entriesObj is not List<object?> entries)
            {
                entries = entriesObj is IEnumerable<object?> seq ? new List<object?>(seq) : new List<object?>();
                root["portals"] = entries;
            }

            mutate(entries);

            var output = Serializer.Serialize(root);
            var parsed = PortalConfigLoader.Parse(output);
            ProviderListValidator.AssertNoDuplicates(parsed);

            AtomicWriteText(ctx.PortalsPath, output);
        }
    }

    private static void ApplyUpsert(IDictionary<object, object?> node, ProviderUpsert req, int id)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            throw new ConfigException("provider name must not be empty");

        var type = (req.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(type) || !Enum.TryParse<PortalType>(type, ignoreCase: true, out _))
            throw new ConfigException($"provider '{name}': type must be one of [api, rss, html, manual], got '{req.Type}'");

        var enabled = req.Enabled ?? true;
        var rate = req.RateLimitRps ?? 1.0;
        if (rate < 0) throw new ConfigException($"provider '{name}': rateLimitRps must be >= 0");

        var endpoint = NullIfBlank(req.Endpoint);
        if (endpoint is not null && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            throw new ConfigException($"provider '{name}': endpoint must be an absolute URL");

        node["id"] = id;
        node["name"] = name;
        node["type"] = type;
        node["enabled"] = enabled;
        SetOrRemove(node, "endpoint", endpoint);
        node["rate_limit_rps"] = rate;
        SetOrRemove(node, "notes", NullIfBlank(req.Notes));
    }

    private static bool TryGetId(IDictionary<object, object?> map, out int id)
    {
        id = 0;
        if (!map.TryGetValue("id", out var v) || v is null) return false;
        return int.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, out id);
    }

    private static void SetOrRemove(IDictionary<object, object?> node, string key, string? value)
    {
        if (value is null) node.Remove(key);
        else node[key] = value;
    }

    private static string? NullIfBlank(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void AtomicWriteText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private sealed record LastFetch(DateTimeOffset FetchedAt, int? FetchedCount);

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
            }
        }

        return result;
    }

    private static IReadOnlyList<ProviderRecentRun> LoadRecentRuns(string historyDir, string providerName, int take)
    {
        var result = new List<ProviderRecentRun>();
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

                    result.Add(new ProviderRecentRun(
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

public sealed record CreateResponse(bool Success, int Id, string? Error = null);
