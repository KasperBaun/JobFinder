using System.Text.RegularExpressions;

namespace Jobmatch.Services;

/// <summary>
/// Company career pages are almost never the board itself — they are marketing pages that link out
/// to the ATS ("See all openings" → Oracle/Greenhouse/…). Pasting the careers page is what people
/// actually do, so when pattern matching comes up empty we read the page once and re-run detection
/// over the links it contains. Read-only, single request, no crawling past the first page.
/// </summary>
public sealed partial class SourceDiscoveryService : ISourceDiscoveryService, IDisposable
{
    // Enough to cover a marketing page's markup; anything larger is not a careers page worth
    // scanning, and reading it in full would be an unbounded download from a stranger's server.
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxLinksScanned = 400;

    private readonly ISourceDetectionService _detection;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public SourceDiscoveryService(ISourceDetectionService detection, HttpMessageHandler? handler = null)
    {
        _detection = detection;
        _ownsHttp = handler is null;
        _http = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: _ownsHttp)
        {
            Timeout = TimeSpan.FromSeconds(20),
            MaxResponseContentBufferSize = MaxBytes,
        };
        // Some careers pages serve a stripped page (or a 403) to clients without a browser UA.
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (compatible; jobfinder/1.0; +local)");
    }

    public async Task<IReadOnlyList<SourceCandidate>> DiscoverAsync(Uri pageUrl, CancellationToken ct)
    {
        var html = await ReadPageAsync(pageUrl, ct).ConfigureAwait(false);
        if (html is null) return [];

        var brand = BrandFromHost(pageUrl.Host);
        if (brand is not null) brand = SourceDetectionService.PrettifyBrand(brand);
        var byKind = new Dictionary<string, SourceCandidate>(StringComparer.Ordinal);

        foreach (var link in LinkCandidates(html, pageUrl))
        {
            foreach (var candidate in _detection.Detect(link))
            {
                // A feed on the company's own domain is a real find, but an ATS board is the better
                // source when a page offers both — so never let RSS displace one.
                if (byKind.ContainsKey(candidate.Kind)) continue;
                byKind[candidate.Kind] = SourceDetectionService.WithBrand(candidate, brand);
            }
        }

        return [.. byKind.Values.OrderBy(c => c.Kind == "rss" ? 1 : 0)];
    }

    private async Task<string?> ReadPageAsync(Uri pageUrl, CancellationToken ct)
    {
        try
        {
            using var response = await _http
                .GetAsync(pageUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return null;

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IEnumerable<Uri> LinkCandidates(string html, Uri pageUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;

        foreach (Match m in HrefPattern().Matches(Unescape(html)))
        {
            if (scanned >= MaxLinksScanned) yield break;
            var raw = m.Groups["url"].Value;
            if (raw.Length == 0) continue;
            if (!Uri.TryCreate(pageUrl, raw, out var uri)) continue;
            if (uri.Scheme is not ("http" or "https")) continue;
            if (!seen.Add(uri.AbsoluteUri)) continue;
            scanned++;
            yield return uri;
        }
    }

    // Careers pages routinely hide the board link in a JSON payload, where the slashes are escaped
    // ("https:\/\/…") and the ampersands entity-encoded. Undo both before matching, or the one link
    // that matters is the one link the scan misses.
    private static string Unescape(string html) =>
        html.Replace("\\/", "/", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&#47;", "/", StringComparison.Ordinal);

    // Second-level label of the host: "careers.danskebank.com" → "danskebank". The ATS URL itself
    // usually carries only an opaque tenant id, so the page we crawled is the better name source.
    private static string? BrandFromHost(string host)
    {
        var labels = host.ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l != "www").ToArray();
        if (labels.Length < 2) return null;
        var label = labels.Length >= 3 && labels[^2].Length <= 3 ? labels[^3] : labels[^2];
        return label.Length < 2 ? null : label;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    // href="…" (possibly relative), and any bare absolute URL — the latter covers the JSON payloads
    // and data attributes that single-page careers sites hand their front end.
    [GeneratedRegex(
        """href\s*=\s*["'](?<url>[^"'\s<>]+)|(?<url>https?://[^"'\s<>()\\]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HrefPattern();
}
