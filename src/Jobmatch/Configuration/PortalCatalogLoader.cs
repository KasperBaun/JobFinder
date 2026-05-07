using System.Text.Json;
using Jobmatch.Models;

namespace Jobmatch.Configuration;

public static class PortalCatalogLoader
{
    public static IReadOnlyList<PortalConfig> Load(string path)
        => Parse(File.ReadAllText(path));

    public static IReadOnlyList<PortalConfig> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("providers", out var providersEl)
            || providersEl.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigException("portals.json: top-level 'providers' array is required");
        }

        var list = new List<PortalConfig>(providersEl.GetArrayLength());
        var seenIds = new HashSet<int>();
        var index = 0;
        foreach (var el in providersEl.EnumerateArray())
        {
            var p = BuildPortal(el, index);
            if (!seenIds.Add(p.Id))
                throw new ConfigException($"portals.json: duplicate id {p.Id} (provider '{p.Name}')");
            list.Add(p);
            index++;
        }
        return list;
    }

    private static PortalConfig BuildPortal(JsonElement el, int index)
    {
        var prefix = $"providers[{index}]";

        var name = RequireString(el, "name", prefix);
        var typeRaw = RequireString(el, "type", prefix);
        if (!Enum.TryParse<PortalType>(typeRaw, ignoreCase: true, out var type))
        {
            throw new ConfigException($"{prefix} ('{name}'): type must be one of [api, rss, html, manual], got '{typeRaw}'");
        }

        var enabled = ReadBool(el, "enabled", true);
        var endpoint = ReadUri(el, "endpoint", name);
        var id = ReadInt(el, "id", 0);
        var rateLimit = ReadDouble(el, "rateLimitRps", 1.0);
        var notes = el.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.String
            ? notesEl.GetString()
            : null;
        var method = el.TryGetProperty("method", out var methodEl) && methodEl.ValueKind == JsonValueKind.String
            ? methodEl.GetString()
            : null;
        var requiresSecret = el.TryGetProperty("requiresSecret", out var rsEl) && rsEl.ValueKind == JsonValueKind.String
            ? rsEl.GetString()
            : null;
        var displayName = el.TryGetProperty("displayName", out var dnEl) && dnEl.ValueKind == JsonValueKind.String
            ? dnEl.GetString()
            : null;

        var queryParams = ReadObjectDict(el, "queryParams");
        var headers = ReadStringDict(el, "headers");
        var responseMapping = ReadStringDict(el, "responseMapping");
        var staticFields = ReadStringDict(el, "staticFields");
        var bodyTemplate = ReadObjectDict(el, "bodyTemplate");
        var pagination = ReadPagination(el, name);
        var html = ReadHtmlSelectors(el, name, type);

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
            Pagination: pagination,
            Id: id,
            RequiresSecret: requiresSecret,
            DisplayName: displayName);
    }

    private static PaginationConfig? ReadPagination(JsonElement el, string portalName)
    {
        if (!el.TryGetProperty("pagination", out var pag) || pag.ValueKind != JsonValueKind.Object)
            return null;

        var param = RequireString(pag, "param", $"portal '{portalName}'.pagination");
        var start = ReadInt(pag, "start", 1);
        var step = ReadInt(pag, "step", 1);

        string? sizeParam = null;
        if (pag.TryGetProperty("sizeParam", out var spEl) && spEl.ValueKind == JsonValueKind.String)
        {
            var s = spEl.GetString();
            if (!string.IsNullOrWhiteSpace(s)) sizeParam = s;
        }

        int? size = null;
        if (pag.TryGetProperty("size", out var szEl) && szEl.ValueKind == JsonValueKind.Number
            && szEl.TryGetInt32(out var szInt))
        {
            size = szInt;
        }

        var maxPages = ReadInt(pag, "maxPages", 5);
        if (maxPages < 1)
            throw new ConfigException($"portal '{portalName}'.pagination: maxPages must be >= 1, got {maxPages}");

        return new PaginationConfig(param, start, step, sizeParam, size, maxPages);
    }

    private static HtmlSelectors? ReadHtmlSelectors(JsonElement el, string portalName, PortalType type)
    {
        if (!el.TryGetProperty("html", out var h) || h.ValueKind != JsonValueKind.Object)
            return null;

        var listSelector = ReadStringProp(h, "listSelector");
        if (string.IsNullOrWhiteSpace(listSelector))
        {
            if (type == PortalType.Html)
                throw new ConfigException($"portal '{portalName}': html.listSelector is required for html portals");
            return null;
        }

        var titleSelector = ReadStringProp(h, "titleSelector");
        if (string.IsNullOrWhiteSpace(titleSelector))
            throw new ConfigException($"portal '{portalName}': html.titleSelector is required");

        var linkSelector = ReadStringProp(h, "linkSelector");
        var companySelector = ReadStringProp(h, "companySelector");
        var locationSelector = ReadStringProp(h, "locationSelector");
        var descriptionSelector = ReadStringProp(h, "descriptionSelector");
        var urlAttr = ReadStringProp(h, "urlAttribute");

        return new HtmlSelectors(
            ListSelector: listSelector,
            TitleSelector: titleSelector!,
            LinkSelector: string.IsNullOrWhiteSpace(linkSelector) ? null : linkSelector,
            CompanySelector: string.IsNullOrWhiteSpace(companySelector) ? null : companySelector,
            LocationSelector: string.IsNullOrWhiteSpace(locationSelector) ? null : locationSelector,
            DescriptionSelector: string.IsNullOrWhiteSpace(descriptionSelector) ? null : descriptionSelector,
            UrlAttribute: string.IsNullOrWhiteSpace(urlAttr) ? "href" : urlAttr);
    }

    private static string? ReadStringProp(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static string RequireString(JsonElement el, string key, string prefix)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Undefined)
            throw new ConfigException($"{prefix}: missing required field '{key}'");
        var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
        if (string.IsNullOrWhiteSpace(s))
            throw new ConfigException($"{prefix}: '{key}' must not be empty");
        return s;
    }

    private static bool ReadBool(JsonElement el, string key, bool defaultValue)
    {
        if (!el.TryGetProperty(key, out var v)) return defaultValue;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue,
        };
    }

    private static int ReadInt(JsonElement el, string key, int defaultValue)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Number) return defaultValue;
        return v.TryGetInt32(out var i) ? i : defaultValue;
    }

    private static double ReadDouble(JsonElement el, string key, double defaultValue)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Number) return defaultValue;
        return v.TryGetDouble(out var d) ? d : defaultValue;
    }

    private static Uri? ReadUri(JsonElement el, string key, string portal)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String) return null;
        var s = v.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            throw new ConfigException($"portal '{portal}': '{key}' is not a valid absolute URL ('{s}')");
        return uri;
    }

    private static IReadOnlyDictionary<string, object?>? ReadObjectDict(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return null;
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in v.EnumerateObject())
        {
            result[prop.Name] = JsonValueToObject(prop.Value);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string>? ReadStringDict(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in v.EnumerateObject())
        {
            var val = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? string.Empty
                : prop.Value.GetRawText();
            result[prop.Name] = val;
        }
        return result;
    }

    private static object? JsonValueToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Number => el.TryGetInt32(out var i) ? (object?)i : el.GetDouble(),
        _ => el,
    };
}
