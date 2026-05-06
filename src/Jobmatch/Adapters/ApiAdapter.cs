using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class ApiAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    private static readonly Regex EndpointPlaceholder = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
        {
            throw new ConfigException($"portal '{PortalName}': api adapter requires 'endpoint'");
        }

        var (renderedEndpoint, baseQueryParams) = RenderEndpointTemplate(Config.Endpoint, Config.QueryParams, PortalName);
        var method = ParseHttpMethod(Config.Method, PortalName);
        var mapping = Config.ResponseMapping ?? throw new ConfigException($"portal '{PortalName}': api adapter requires 'response_mapping'");

        if (Config.Pagination is null)
        {
            return await FetchOnePageAsync(renderedEndpoint, baseQueryParams, Config.BodyTemplate, method, mapping, ct);
        }

        var p = Config.Pagination;
        var isPost = method == HttpMethod.Post;
        var all = new List<Listing>();
        var current = p.Start;
        for (var pageIdx = 0; pageIdx < p.MaxPages; pageIdx++, current += p.Step)
        {
            var (qp, body) = InjectPagination(baseQueryParams, Config.BodyTemplate, isPost, p, current);
            var pageResults = await FetchOnePageAsync(renderedEndpoint, qp, body, method, mapping, ct);
            if (pageResults.Count == 0) break;
            all.AddRange(pageResults);
            if (p.Size is int sz && pageResults.Count < sz) break;
        }
        return all;
    }

    private async Task<IReadOnlyList<Listing>> FetchOnePageAsync(
        Uri renderedEndpoint,
        IReadOnlyDictionary<string, object?>? queryParams,
        IReadOnlyDictionary<string, object?>? bodyTemplate,
        HttpMethod method,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken ct)
    {
        var uri = BuildRequestUri(renderedEndpoint, queryParams);
        Logger.LogInformation("portal={Portal} {Method} {Uri}", PortalName, method.Method, uri);

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("application/json");
        if (Config.Headers is not null)
        {
            foreach (var kvp in Config.Headers)
            {
                if (string.Equals(kvp.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }
        if (method == HttpMethod.Post && bodyTemplate is not null)
        {
            request.Content = JsonContent.Create(bodyTemplate);
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

    private static (IReadOnlyDictionary<string, object?>? qp, IReadOnlyDictionary<string, object?>? body) InjectPagination(
        IReadOnlyDictionary<string, object?>? queryParams,
        IReadOnlyDictionary<string, object?>? bodyTemplate,
        bool isPost,
        PaginationConfig p,
        int current)
    {
        if (isPost)
        {
            var newBody = bodyTemplate is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(bodyTemplate, StringComparer.Ordinal);
            newBody[p.Param] = current;
            if (p.SizeParam is not null && p.Size is int bodySize) newBody[p.SizeParam] = bodySize;
            return (queryParams, newBody);
        }
        var newQp = queryParams is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(queryParams, StringComparer.Ordinal);
        newQp[p.Param] = current;
        if (p.SizeParam is not null && p.Size is int qpSize) newQp[p.SizeParam] = qpSize;
        return (newQp, bodyTemplate);
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

    private static (Uri Endpoint, IReadOnlyDictionary<string, object?>? QueryParams) RenderEndpointTemplate(
        Uri endpoint, IReadOnlyDictionary<string, object?>? queryParams, string portalName)
    {
        var template = endpoint.OriginalString;
        if (!template.Contains('{')) return (endpoint, queryParams);

        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var rendered = EndpointPlaceholder.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (queryParams is null || !queryParams.TryGetValue(key, out var val) || val is null)
            {
                throw new ConfigException(
                    $"portal '{portalName}': endpoint template references '{{{key}}}' but no matching key in query_params.");
            }
            consumed.Add(key);
            return Uri.EscapeDataString(Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty);
        });

        var renderedUri = new Uri(rendered, UriKind.Absolute);
        if (queryParams is null || consumed.Count == 0) return (renderedUri, queryParams);

        var remaining = queryParams
            .Where(kvp => !consumed.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        return (renderedUri, remaining.Count == 0 ? null : remaining);
    }

    private static HttpMethod ParseHttpMethod(string? method, string portalName)
    {
        if (string.IsNullOrWhiteSpace(method)) return HttpMethod.Get;
        return method.Trim().ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new ConfigException(
                $"portal '{portalName}': method must be one of [get, post], got '{method}'."),
        };
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

    private string RenderTemplate(string template, JsonElement item)
    {
        var result = template;
        var start = result.IndexOf('{');
        while (start >= 0)
        {
            var end = result.IndexOf('}', start);
            if (end < 0) break;
            var key = result.Substring(start + 1, end - start - 1);
            string value;
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var prop))
            {
                value = prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.ToString(),
                    _ => string.Empty,
                };
            }
            else
            {
                Logger.LogWarning(
                    "portal={Portal} url_template references '{{{Key}}}' which is not in this item; the produced URL will be malformed and the listing will be dropped",
                    PortalName, key);
                value = string.Empty;
            }
            result = string.Concat(result.AsSpan(0, start), value, result.AsSpan(end + 1));
            start = result.IndexOf('{', start + value.Length);
        }
        return result;
    }
}
