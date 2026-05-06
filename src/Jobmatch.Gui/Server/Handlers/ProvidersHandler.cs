using System.Text.Json;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Jobmatch.Models;
using Microsoft.AspNetCore.Http;
using YamlDotNet.Serialization;

namespace Jobmatch.Gui.Server.Handlers;

public static class ProvidersHandler
{
    private static readonly object FileLock = new();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();
    private static readonly ISerializer Serializer = new SerializerBuilder().Build();

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

    public static IResult Put(ProvidersUpdateRequest? req, Jobmatch.UserContext ctx)
    {
        if (req?.Providers is null)
        {
            return Results.Ok(new SaveResponse(false, "providers list is required"));
        }
        try
        {
            lock (FileLock)
            {
                var raw = File.Exists(ctx.PortalsPath)
                    ? File.ReadAllText(ctx.PortalsPath)
                    : string.Empty;

                var existingByName = ParseExistingByName(raw);
                var newList = BuildPortalsList(req.Providers, existingByName);

                var output = SerializePortals(newList);
                _ = PortalConfigLoader.Parse(output);
                AtomicWriteText(ctx.PortalsPath, output);
            }
            GuiLog.Action($"saved portals.yml ({req.Providers.Count} providers)");
            return Results.Ok(new SaveResponse(true));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"PUT /api/providers — {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException is { } inner)
                GuiLog.Error($"    inner — {inner.GetType().Name}: {inner.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    private static Dictionary<string, Dictionary<object, object?>> ParseExistingByName(string yaml)
    {
        var result = new Dictionary<string, Dictionary<object, object?>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(yaml)) return result;

        Dictionary<object, object?>? root;
        try
        {
            root = Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        }
        catch
        {
            return result;
        }
        if (root is null) return result;
        if (!root.TryGetValue("portals", out var raw) || raw is not IEnumerable<object?> entries) return result;

        foreach (var entry in entries)
        {
            if (entry is not IDictionary<object, object?> map) continue;
            var name = map.TryGetValue("name", out var n) ? n?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[name] = new Dictionary<object, object?>(map);
            }
        }
        return result;
    }

    private static List<Dictionary<object, object?>> BuildPortalsList(
        IReadOnlyList<ProviderUpsert> incoming,
        IReadOnlyDictionary<string, Dictionary<object, object?>> existingByName)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Dictionary<object, object?>>();

        foreach (var p in incoming)
        {
            var name = p.Name?.Trim();
            if (string.IsNullOrEmpty(name)) throw new ConfigException("provider name must not be empty");
            if (!seenNames.Add(name)) throw new ConfigException($"duplicate provider name '{name}'");

            var type = p.Type?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type) || !Enum.TryParse<PortalType>(type, ignoreCase: true, out _))
            {
                throw new ConfigException($"provider '{name}': type must be one of [api, rss, html, manual], got '{p.Type}'");
            }

            var enabled = p.Enabled ?? true;
            var rateLimit = p.RateLimitRps ?? 1.0;
            if (rateLimit < 0) throw new ConfigException($"provider '{name}': rateLimitRps must be >= 0");

            var endpoint = NullIfBlank(p.Endpoint);
            if (endpoint is not null && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
                throw new ConfigException($"provider '{name}': endpoint must be an absolute URL");

            var node = existingByName.TryGetValue(name, out var prior)
                ? new Dictionary<object, object?>(prior)
                : new Dictionary<object, object?>();

            node["name"] = name;
            node["type"] = type;
            node["enabled"] = enabled;
            SetOrRemove(node, "endpoint", endpoint);
            node["rate_limit_rps"] = rateLimit;
            SetOrRemove(node, "notes", NullIfBlank(p.Notes));

            list.Add(node);
        }
        return list;
    }

    private static void SetOrRemove(IDictionary<object, object?> node, string key, string? value)
    {
        if (value is null) node.Remove(key);
        else node[key] = value;
    }

    private static string SerializePortals(List<Dictionary<object, object?>> list)
    {
        var root = new Dictionary<object, object?> { ["portals"] = list };
        return Serializer.Serialize(root);
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
}
