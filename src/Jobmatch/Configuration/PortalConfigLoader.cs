using Jobmatch.Models;
using YamlDotNet.Serialization;

namespace Jobmatch.Configuration;

public static class PortalConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    public static IReadOnlyList<PortalConfig> Parse(string yaml)
    {
        Dictionary<object, object?>? root;
        try
        {
            root = Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        }
        catch (Exception ex) when (ex is not ConfigException)
        {
            throw new ConfigException($"portals: YAML parse error — {ex.Message}", ex);
        }

        if (root is null || !root.TryGetValue("portals", out var raw) || raw is not IEnumerable<object?> entries)
        {
            throw new ConfigException("portals: top-level 'portals:' list is missing");
        }

        var list = new List<PortalConfig>();
        var index = 0;
        foreach (var entry in entries)
        {
            if (entry is not IDictionary<object, object?> map)
            {
                throw new ConfigException($"portals[{index}]: expected a mapping");
            }
            list.Add(BuildPortal(NormaliseKeys(map), index));
            index++;
        }
        return list;
    }

    public static IReadOnlyList<PortalConfig> Load(string path) => Parse(File.ReadAllText(path));

    private static PortalConfig BuildPortal(IReadOnlyDictionary<string, object?> map, int index)
    {
        var prefix = $"portals[{index}]";
        var name = RequireString(map, "name", prefix);
        var typeRaw = RequireString(map, "type", prefix);
        if (!Enum.TryParse<PortalType>(typeRaw, ignoreCase: true, out var type))
        {
            throw new ConfigException($"{prefix} ('{name}'): type must be one of [api, rss, html, manual], got '{typeRaw}'");
        }

        var enabled = ReadBool(map, "enabled", true);
        var baseUrl = ReadUri(map, "base_url", name);
        var endpoint = ReadUri(map, "endpoint", name);
        var queryParams = ReadDict(map, "query_params");
        var headers = ReadStringDict(map, "headers");
        var responseMapping = ReadStringDict(map, "response_mapping");
        var rateLimit = ReadDouble(map, "rate_limit_rps", 1.0);
        var notes = map.TryGetValue("notes", out var n) ? n?.ToString() : null;

        return new PortalConfig(
            Name: name,
            Type: type,
            Enabled: enabled,
            BaseUrl: baseUrl,
            Endpoint: endpoint,
            QueryParams: queryParams,
            Headers: headers,
            ResponseMapping: responseMapping,
            RateLimitRps: rateLimit,
            Notes: notes);
    }

    private static IReadOnlyDictionary<string, object?> NormaliseKeys(IDictionary<object, object?> map)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in map)
        {
            var key = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(key)) result[key] = kvp.Value;
        }
        return result;
    }

    private static string RequireString(IReadOnlyDictionary<string, object?> map, string key, string prefix)
    {
        if (!map.TryGetValue(key, out var v) || v is null)
        {
            throw new ConfigException($"{prefix}: missing required field '{key}'");
        }
        var s = v.ToString();
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new ConfigException($"{prefix}: '{key}' must not be empty");
        }
        return s;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return bool.TryParse(v.ToString(), out var b) ? b : defaultValue;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, object?> map, string key, double defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return double.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : defaultValue;
    }

    private static Uri? ReadUri(IReadOnlyDictionary<string, object?> map, string key, string portal)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return null;
        var s = v.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
        {
            throw new ConfigException($"portal '{portal}': '{key}' is not a valid absolute URL ('{s}')");
        }
        return uri;
    }

    private static IReadOnlyDictionary<string, object?>? ReadDict(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is not IDictionary<object, object?> inner) return null;
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in inner)
        {
            var k = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(k)) result[k] = kvp.Value;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string>? ReadStringDict(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is not IDictionary<object, object?> inner) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in inner)
        {
            var k = kvp.Key?.ToString();
            var val = kvp.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(k)) result[k] = val;
        }
        return result;
    }
}
