using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public abstract partial class BaseAdapter(PortalConfig config, HttpClient http, ILogger logger) : IJobPortalAdapter
{
    protected PortalConfig Config { get; } = config;
    protected HttpClient Http { get; } = http;
    protected ILogger Logger { get; } = logger;

    public string PortalName => Config.Name;

    public abstract Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);

    private DateTimeOffset _lastCallAt = DateTimeOffset.MinValue;

    protected async Task ThrottleAsync(CancellationToken ct)
    {
        var rps = Config.RateLimitRps;
        if (rps <= 0) return;
        var minIntervalMs = 1000.0 / rps;
        var elapsed = (DateTimeOffset.UtcNow - _lastCallAt).TotalMilliseconds;
        if (elapsed < minIntervalMs)
        {
            await Task.Delay((int)Math.Ceiling(minIntervalMs - elapsed), ct);
        }
        _lastCallAt = DateTimeOffset.UtcNow;
    }

    protected static string StableId(string portal, string sourceOrUrl)
    {
        var input = $"{portal}:{sourceOrUrl}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(32);
        foreach (var b in bytes.AsSpan(0, 16)) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    protected static string StripHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        // Replace each tag with a single space so `<p>foo</p><ul><li>bar</li></ul>` becomes
        // `foo bar`, not `foobar`. Without this the keyword regex (which uses word boundaries)
        // misses tokens that sit at the seam between adjacent block elements.
        var sb = new StringBuilder(input.Length);
        var inside = false;
        foreach (var ch in input)
        {
            if (ch == '<')
            {
                if (!inside) sb.Append(' ');
                inside = true;
            }
            else if (ch == '>')
            {
                inside = false;
            }
            else if (!inside)
            {
                sb.Append(ch);
            }
        }
        var decoded = System.Net.WebUtility.HtmlDecode(sb.ToString());
        return WhitespaceRun.Replace(decoded, " ").Trim();
    }

    // Drives a single-page fetch delegate across pages when Config.Pagination is set,
    // accumulating de-duplicated listings. Stops at MaxPages, on an empty page, on a
    // page that adds nothing new (server ignored the page param and re-served an earlier
    // page — e.g. a JS-paginated list whose SSR fallback is always page 1), or on a short
    // page (fewer than Size => the last page). With no Pagination it does exactly one
    // fetch with the base query params, so non-paginated providers behave unchanged.
    // Shared by RssAdapter and HtmlAdapter; ApiAdapter has its own loop (it also paginates POST bodies).
    protected async Task<IReadOnlyList<Listing>> FetchPagesAsync(
        Func<IReadOnlyDictionary<string, object?>?, CancellationToken, Task<IReadOnlyList<Listing>>> fetchPage,
        CancellationToken ct)
    {
        var p = Config.Pagination;
        if (p is null) return await fetchPage(Config.QueryParams, ct);

        var all = new List<Listing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = p.Start;
        for (var page = 0; page < p.MaxPages; page++, current += p.Step)
        {
            var pageResults = await fetchPage(MergePageParam(Config.QueryParams, p, current), ct);
            if (pageResults.Count == 0) break;

            var added = 0;
            foreach (var listing in pageResults)
            {
                if (seen.Add(listing.Id)) { all.Add(listing); added++; }
            }
            if (added == 0) break;                                       // page param ignored => duplicate page
            if (p.Size is int size && pageResults.Count < size) break;   // short page => last page
        }
        return all;
    }

    // Copies the base query params and writes the page cursor (and optional page size)
    // for GET-style paginated feeds/scrapes. Mirrors ApiAdapter's GET pagination branch.
    private static IReadOnlyDictionary<string, object?> MergePageParam(
        IReadOnlyDictionary<string, object?>? queryParams, PaginationConfig p, int current)
    {
        var qp = queryParams is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(queryParams, StringComparer.Ordinal);
        qp[p.Param] = current;
        if (p.SizeParam is not null && p.Size is int size) qp[p.SizeParam] = size;
        return qp;
    }

    // Appends Config.QueryParams to the endpoint URL, preserving any pre-existing
    // query string. Used by RssAdapter and any other adapter that doesn't otherwise
    // build a request URI itself.
    protected static Uri AppendQueryParams(Uri endpoint, IReadOnlyDictionary<string, object?>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0) return endpoint;
        var builder = new UriBuilder(endpoint);
        var existing = string.IsNullOrEmpty(builder.Query) ? new List<string>() : new List<string> { builder.Query.TrimStart('?') };
        foreach (var kvp in queryParams)
        {
            if (kvp.Value is null) continue;
            var val = Convert.ToString(kvp.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            existing.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(val)}");
        }
        builder.Query = string.Join("&", existing);
        return builder.Uri;
    }

    protected static RemoteMode InferRemoteMode(string text)
    {
        if (string.IsNullOrEmpty(text)) return RemoteMode.Unknown;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("hybrid")) return RemoteMode.Hybrid;
        if (lower.Contains("fully remote") || lower.Contains("100% remote") || lower.Contains("remote-only") || lower.Contains("remote only") || lower.Contains("work from home") || lower.Contains("wfh") || lower.Contains("fjernarbejde")) return RemoteMode.Remote;
        if (lower.Contains("onsite") || lower.Contains("on-site") || lower.Contains("in-office")) return RemoteMode.Onsite;
        if (lower.Contains("remote")) return RemoteMode.Remote;
        // Danish: "hjemmearbejde" / "hjemmefra" appear as "mulighed for hjemmearbejde"
        // (possibility of WFH) — Danish ads use this for hybrid setups, not full remote.
        if (lower.Contains("hjemmearbejde") || lower.Contains("hjemmefra")) return RemoteMode.Hybrid;
        return RemoteMode.Unknown;
    }

    protected Listing BuildListing(
        string sourceId,
        string title,
        string? company,
        string? location,
        string description,
        Uri url,
        DateTimeOffset? postedAt,
        JsonElement raw)
    {
        if (Config.StaticFields is { } sf)
        {
            if (sf.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t)) title = t;
            if (sf.TryGetValue("company", out var c) && !string.IsNullOrWhiteSpace(c)) company = c;
            if (sf.TryGetValue("location", out var l) && !string.IsNullOrWhiteSpace(l)) location = l;
        }

        return new Listing(
            Id: StableId(Config.Name, sourceId),
            Portal: Config.Name,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: InferRemoteMode($"{title} {description} {location}"),
            Description: description,
            Url: url,
            PostedAt: postedAt,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: raw);
    }
}
