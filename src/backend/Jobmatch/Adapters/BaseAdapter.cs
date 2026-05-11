using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public abstract class BaseAdapter(PortalConfig config, HttpClient http, ILogger logger) : IJobPortalAdapter
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

    // Delay between body-enrichment fetches when EnrichBody is on. ~5rps so a 100-item
    // run takes ~20s of body fetching, which the user sees as the active provider phase
    // taking longer. Sequential, not concurrent — keeps a single host from getting hit
    // too hard while staying faster than the portal's own RateLimitRps (which is set
    // very low to avoid rate-limit on the catalog endpoint).
    protected const int BodyFetchDelayMs = 200;

    // Fetches each listing's URL and merges its visible text into Description. Sequential
    // with a small delay so we don't hammer the host. Failures are logged but don't drop
    // the listing — we keep the catalog-only version. Shared by RssAdapter (after feed
    // parse) and ApiAdapter (after items_path mapping) when Config.EnrichBody is true.
    //
    // For Jobindex preview pages (jobindex.dk/vis-job/*), the linked page is just a
    // Jobindex-branded wrapper around the employer's actual ATS posting. We detect those
    // and follow the embedded "see job" link to fetch the real description.
    internal async Task<IReadOnlyList<Listing>> EnrichBodiesAsync(IReadOnlyList<Listing> listings, CancellationToken ct)
    {
        var enriched = new List<Listing>(listings.Count);
        for (var i = 0; i < listings.Count; i++)
        {
            var listing = listings[i];
            try
            {
                if (i > 0) await Task.Delay(BodyFetchDelayMs, ct);
                var previewHtml = await FetchBodyHtmlAsync(listing.Url, ct);
                // For Jobindex/it-jobbank preview pages, the area span ('jix_robotjob--area')
                // carries the actual location; the original RSS feed item lacked one.
                var fromPreview = ExtractJobindexLocation(listing.Url, previewHtml);
                if (!string.IsNullOrWhiteSpace(fromPreview) && string.IsNullOrWhiteSpace(listing.Location))
                {
                    listing = listing with { Location = fromPreview };
                }

                var bodyHtml = previewHtml;
                var externalHref = ExtractJobindexExternalLink(listing.Url, previewHtml);
                if (externalHref is not null && Uri.TryCreate(externalHref, UriKind.Absolute, out var external))
                {
                    await Task.Delay(BodyFetchDelayMs, ct);
                    var externalHtml = await FetchBodyHtmlAsync(external, ct);
                    if (!string.IsNullOrWhiteSpace(externalHtml))
                    {
                        bodyHtml = externalHtml;
                    }
                }
                enriched.Add(MergeBodyHtml(listing, bodyHtml));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} body fetch failed for {Url}", PortalName, listing.Url);
                enriched.Add(listing);
            }
        }
        return enriched;
    }

    private static readonly System.Text.RegularExpressions.Regex JobindexAreaSpan = new(
        @"<span[^>]*\bclass\s*=\s*[""'][^""']*jix_robotjob--area[^""']*[""'][^>]*>([^<]+)</span>",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? ExtractJobindexLocation(Uri listingUrl, string? previewHtml)
    {
        if (string.IsNullOrWhiteSpace(previewHtml)) return null;
        if (!listingUrl.Host.Contains("jobindex.dk", StringComparison.OrdinalIgnoreCase)
            && !listingUrl.Host.Contains("it-jobbank.dk", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = JobindexAreaSpan.Match(previewHtml);
        if (!match.Success) return null;
        var text = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static readonly System.Text.RegularExpressions.Regex JobindexExternalLink = new(
        @"<a[^>]*\bclass\s*=\s*[""'][^""']*seejobdesktop[^""']*[""'][^>]*\bhref\s*=\s*[""']([^""']+)[""']",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? ExtractJobindexExternalLink(Uri listingUrl, string? bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(bodyHtml)) return null;
        if (!listingUrl.Host.Contains("jobindex.dk", StringComparison.OrdinalIgnoreCase)
            && !listingUrl.Host.Contains("it-jobbank.dk", StringComparison.OrdinalIgnoreCase))
            return null;
        var match = JobindexExternalLink.Match(bodyHtml);
        if (!match.Success) return null;
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task<string?> FetchBodyHtmlAsync(Uri url, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
        if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;
        return await response.Content.ReadAsStringAsync(ct);
    }

    internal static Listing MergeBodyHtml(Listing original, string? bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(bodyHtml)) return original;
        var bodyText = StripHtml(bodyHtml);
        if (string.IsNullOrWhiteSpace(bodyText)) return original;
        var combined = string.IsNullOrEmpty(original.Description)
            ? bodyText
            : original.Description + " " + bodyText;
        return original with { Description = combined };
    }

    protected static RemoteMode InferRemoteMode(string text)
    {
        if (string.IsNullOrEmpty(text)) return RemoteMode.Unknown;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("hybrid")) return RemoteMode.Hybrid;
        if (lower.Contains("fully remote") || lower.Contains("100% remote") || lower.Contains("remote-only") || lower.Contains("remote only") || lower.Contains("work from home") || lower.Contains("wfh")) return RemoteMode.Remote;
        if (lower.Contains("onsite") || lower.Contains("on-site") || lower.Contains("in-office")) return RemoteMode.Onsite;
        if (lower.Contains("remote")) return RemoteMode.Remote;
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
