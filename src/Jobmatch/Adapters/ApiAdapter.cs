using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class ApiAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
        {
            throw new ConfigException($"portal '{PortalName}': api adapter requires 'endpoint'");
        }

        var uri = BuildRequestUri(Config.Endpoint, Config.QueryParams);
        Logger.LogInformation("portal={Portal} GET {Uri}", PortalName, uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("application/json");
        if (Config.Headers is not null)
        {
            foreach (var kvp in Config.Headers)
            {
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not null && !LooksLikeJson(contentType))
        {
            throw new ConfigException(
                $"portal '{PortalName}': endpoint returned '{contentType}', expected JSON. " +
                "The portal's API likely changed, requires authentication, or is the wrong URL.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await ParseJsonOrThrow(stream, PortalName, ct);

        var mapping = Config.ResponseMapping ?? throw new ConfigException($"portal '{PortalName}': api adapter requires 'response_mapping'");
        var itemsPath = mapping.TryGetValue("items_path", out var ip) ? ip : null;
        var items = WalkJsonPath(doc.RootElement, itemsPath);
        if (items.ValueKind != JsonValueKind.Array)
        {
            Logger.LogWarning("portal={Portal} items_path did not resolve to an array; got {Kind}", PortalName, items.ValueKind);
            return [];
        }

        var results = new List<Listing>();
        foreach (var item in items.EnumerateArray())
        {
            var listing = TryBuildListing(item, mapping);
            if (listing is not null) results.Add(listing);
        }
        return results;
    }

    private static bool LooksLikeJson(string mediaType) =>
        mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
        mediaType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonDocument> ParseJsonOrThrow(Stream stream, string portalName, CancellationToken ct)
    {
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            throw new ConfigException(
                $"portal '{portalName}': response was not valid JSON ({ex.Message}). " +
                "The endpoint may have changed or be returning an HTML/error page.", ex);
        }
    }

    private static Uri BuildRequestUri(Uri endpoint, IReadOnlyDictionary<string, object?>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0) return endpoint;
        var builder = new UriBuilder(endpoint);
        var existing = string.IsNullOrEmpty(builder.Query) ? new List<string>() : new List<string> { builder.Query.TrimStart('?') };
        foreach (var kvp in queryParams)
        {
            if (kvp.Value is null) continue;
            var val = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            existing.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(val)}");
        }
        builder.Query = string.Join("&", existing);
        return builder.Uri;
    }

    private static JsonElement WalkJsonPath(JsonElement root, string? dottedPath)
    {
        if (string.IsNullOrEmpty(dottedPath)) return root;
        var current = root;
        foreach (var segment in dottedPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return default;
            }
            current = next;
        }
        return current;
    }

    private Listing? TryBuildListing(JsonElement item, IReadOnlyDictionary<string, string> mapping)
    {
        try
        {
            var sourceId = ReadString(item, mapping, "id");
            var title = ReadString(item, mapping, "title") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title)) return null;

            var company = ReadString(item, mapping, "company");
            var location = ReadString(item, mapping, "location");
            var descriptionRaw = ReadString(item, mapping, "description") ?? string.Empty;
            var description = StripHtml(descriptionRaw);

            Uri? url = null;
            if (mapping.TryGetValue("url", out var urlField))
            {
                var urlStr = ReadString(item, mapping, "url");
                if (!string.IsNullOrWhiteSpace(urlStr) && Uri.TryCreate(urlStr, UriKind.Absolute, out var parsed)) url = parsed;
            }
            else if (mapping.TryGetValue("url_template", out var tpl))
            {
                var rendered = RenderTemplate(tpl, item);
                if (Uri.TryCreate(rendered, UriKind.Absolute, out var parsed)) url = parsed;
            }
            if (url is null) return null;

            DateTimeOffset? postedAt = null;
            var postedStr = ReadString(item, mapping, "posted_at");
            if (!string.IsNullOrWhiteSpace(postedStr) && DateTimeOffset.TryParse(postedStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
            {
                postedAt = parsedDate;
            }

            return BuildListing(
                sourceId: sourceId ?? url.ToString(),
                title: title.Trim(),
                company: string.IsNullOrWhiteSpace(company) ? null : company!.Trim(),
                location: string.IsNullOrWhiteSpace(location) ? null : location!.Trim(),
                description: description,
                url: url,
                postedAt: postedAt,
                raw: item.Clone());
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "portal={Portal} skipped malformed item", PortalName);
            return null;
        }
    }

    private static string? ReadString(JsonElement item, IReadOnlyDictionary<string, string> mapping, string field)
    {
        if (!mapping.TryGetValue(field, out var path)) return null;
        var el = WalkJsonPath(item, path);
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True or JsonValueKind.False => el.ToString(),
            _ => null,
        };
    }

    private static string RenderTemplate(string template, JsonElement item)
    {
        var result = template;
        var start = result.IndexOf('{');
        while (start >= 0)
        {
            var end = result.IndexOf('}', start);
            if (end < 0) break;
            var key = result.Substring(start + 1, end - start - 1);
            var value = item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var prop)
                ? prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.ToString(),
                    _ => string.Empty,
                }
                : string.Empty;
            result = string.Concat(result.AsSpan(0, start), value, result.AsSpan(end + 1));
            start = result.IndexOf('{', start + value.Length);
        }
        return result;
    }
}
