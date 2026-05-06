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
        var endpoint = ReadUri(map, "endpoint", name);
        var queryParams = ReadDict(map, "query_params");
        var headers = ReadStringDict(map, "headers");
        var responseMapping = ReadStringDict(map, "response_mapping");
        var rateLimit = ReadDouble(map, "rate_limit_rps", 1.0);
        var notes = map.TryGetValue("notes", out var n) ? n?.ToString() : null;
        var html = ReadHtmlSelectors(map, name, type);
        var staticFields = ReadStringDict(map, "static_fields");
        var method = map.TryGetValue("method", out var m) ? m?.ToString() : null;
        var bodyTemplate = ReadDict(map, "body_template");
        var pagination = ReadPagination(map, name);

        return new PortalConfig(
            Name: name,
            Type: type,
            Enabled: enabled,
            Endpoint: endpoint,
            QueryParams: queryParams,
            Headers: headers,
            ResponseMapping: responseMapping,
            Html: html,
            RateLimitRps: rateLimit,
            Notes: notes,
            StaticFields: staticFields,
            Method: method,
            BodyTemplate: bodyTemplate,
            Pagination: pagination);
    }

    private static PaginationConfig? ReadPagination(IReadOnlyDictionary<string, object?> map, string portalName)
    {
        if (!map.TryGetValue("pagination", out var v) || v is not IDictionary<object, object?> inner) return null;
        var p = NormaliseKeys(inner);
        var param = RequireString(p, "param", $"portal '{portalName}'.pagination");
        var start = ReadInt(p, "start", 1);
        var step = ReadInt(p, "step", 1);
        var sizeParam = p.TryGetValue("size_param", out var spv) ? spv?.ToString() : null;
        if (string.IsNullOrWhiteSpace(sizeParam)) sizeParam = null;
        int? size = null;
        if (p.TryGetValue("size", out var szv) && int.TryParse(szv?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var szInt))
        {
            size = szInt;
        }
        var maxPages = ReadInt(p, "max_pages", 5);
        if (maxPages < 1)
        {
            throw new ConfigException($"portal '{portalName}'.pagination: max_pages must be >= 1, got {maxPages}");
        }
        return new PaginationConfig(param, start, step, sizeParam, size, maxPages);
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> map, string key, int defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return int.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : defaultValue;
    }

    private static HtmlSelectors? ReadHtmlSelectors(IReadOnlyDictionary<string, object?> map, string portalName, PortalType type)
    {
        if (!map.TryGetValue("html", out var v) || v is not IDictionary<object, object?> inner) return null;
        var h = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in inner)
        {
            var k = kvp.Key?.ToString();
            var val = kvp.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(k)) h[k] = val;
        }

        if (!h.TryGetValue("list_selector", out var list) || string.IsNullOrWhiteSpace(list))
        {
            if (type == PortalType.Html)
            {
                throw new ConfigException($"portal '{portalName}': html.list_selector is required for html portals");
            }
            return null;
        }
        h.TryGetValue("title_selector", out var title);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ConfigException($"portal '{portalName}': html.title_selector is required");
        }

        h.TryGetValue("link_selector", out var link);
        h.TryGetValue("company_selector", out var company);
        h.TryGetValue("location_selector", out var location);
        h.TryGetValue("description_selector", out var description);
        h.TryGetValue("url_attribute", out var urlAttr);

        return new HtmlSelectors(
            ListSelector: list,
            TitleSelector: title!,
            LinkSelector: string.IsNullOrWhiteSpace(link) ? null : link,
            CompanySelector: string.IsNullOrWhiteSpace(company) ? null : company,
            LocationSelector: string.IsNullOrWhiteSpace(location) ? null : location,
            DescriptionSelector: string.IsNullOrWhiteSpace(description) ? null : description,
            UrlAttribute: string.IsNullOrWhiteSpace(urlAttr) ? "href" : urlAttr);
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
