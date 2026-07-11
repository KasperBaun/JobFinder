using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

// Body enrichment: after an adapter produces catalog listings, optionally fetch each listing's
// page and merge its visible text into Description (Config.EnrichBody). Split out of BaseAdapter
// so the base file stays within the file-size limit.
public abstract partial class BaseAdapter
{
    // Per-fetch stagger for adapters that crawl detail pages sequentially (TeamTailor's page-per-job
    // sitemap walk). Body enrichment no longer uses it — its SemaphoreSlim gate alone bounds the peak
    // rate to any single host, and the stagger on top was the dominant drag on large feeds.
    protected const int BodyFetchDelayMs = 200;

    // Bounded concurrency for body enrichment. Sequential fetching made wall-clock scale linearly
    // with listing count (a 160-item feed doing two fetches each ran minutes and, under the run's
    // Task.WhenAll, held up every other source). The gate alone bounds the peak rate to any single
    // host, so fetches now go as fast as the gate allows.
    private const int EnrichConcurrency = 10;

    // Body pages are single documents a healthy host serves in a couple of seconds; the shared
    // client's 30s timeout is sized for catalog fetches. Enrichment fails faster — a lost body
    // only costs description text, never the listing.
    private static readonly TimeSpan EnrichFetchTimeout = TimeSpan.FromSeconds(10);

    // After this many consecutive failures against one host, the remaining enrichment fetches to
    // it are skipped for the rest of the run. A tarpitting host (accepts connections, never
    // responds) otherwise burns the per-source budget one timeout at a time. The count lives on
    // the shared BodyFetchSession, so one source's discovery spares every other source the cost.
    private const int MaxConsecutiveHostFailures = 3;

    // Fetches each listing's URL and merges its visible text into Description. Runs with a small
    // bounded concurrency so we don't hammer the host. Failures are logged but don't drop the
    // listing — we keep the catalog-only version. Output order matches input.
    // Shared by RssAdapter (after feed parse) and ApiAdapter (after items_path mapping) when
    // Config.EnrichBody is true.
    //
    // For Jobindex preview pages (jobindex.dk/vis-job/*), the linked page is just a
    // Jobindex-branded wrapper around the employer's actual ATS posting. We detect those
    // and follow the embedded "see job" link to fetch the real description.
    internal async Task<IReadOnlyList<Listing>> EnrichBodiesAsync(IReadOnlyList<Listing> listings, CancellationToken ct)
    {
        var enriched = new Listing[listings.Count];
        using var gate = new SemaphoreSlim(EnrichConcurrency);
        var session = FetchSession ?? new BodyFetchSession();
        var skippedByHost = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var tasks = new Task[listings.Count];
        for (var i = 0; i < listings.Count; i++)
        {
            var index = i;
            tasks[index] = RunOne(index);
        }
        await Task.WhenAll(tasks);

        foreach (var (host, count) in skippedByHost)
        {
            Logger.LogWarning(
                "portal={Portal} skipped {Count} body fetches to {Host} — {Threshold} consecutive failures this run, listings kept without body text",
                PortalName, count, host, MaxConsecutiveHostFailures);
        }
        return enriched;

        async Task RunOne(int index)
        {
            await gate.WaitAsync(ct);
            var listing = listings[index];
            try
            {
                enriched[index] = await EnrichOneAsync(listing, session, skippedByHost, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} body enrichment failed for {Url}", PortalName, listing.Url);
                enriched[index] = listing;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private async Task<Listing> EnrichOneAsync(
        Listing listing, BodyFetchSession session, ConcurrentDictionary<string, int> skippedByHost, CancellationToken ct)
    {
        var previewHtml = await TryFetchBodyHtmlAsync(listing.Url, session, skippedByHost, ct);
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
            var externalHtml = await TryFetchBodyHtmlAsync(external, session, skippedByHost, ct);
            if (!string.IsNullOrWhiteSpace(externalHtml))
            {
                bodyHtml = externalHtml;
            }
        }
        return MergeBodyHtml(listing, bodyHtml);
    }

    // Guarded fetch: consults the session's per-host breaker, serves repeats from its run-wide
    // page cache, and bounds each live request to EnrichFetchTimeout. Timeouts and HTTP-level
    // failures are the expected kind here — logged as one line, no stack trace — and return null
    // so the caller keeps the catalog-only listing.
    private async Task<string?> TryFetchBodyHtmlAsync(
        Uri url, BodyFetchSession session, ConcurrentDictionary<string, int> skippedByHost, CancellationToken ct)
    {
        if (session.IsHostTripped(url.Host, MaxConsecutiveHostFailures))
        {
            skippedByHost.AddOrUpdate(url.Host, 1, (_, n) => n + 1);
            return null;
        }
        try
        {
            var html = await session.GetOrFetchAsync(url, u => FetchBodyHtmlWithTimeoutAsync(u, ct));
            session.RecordSuccess(url.Host);
            return html;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            session.RecordFailure(url.Host);
            Logger.LogWarning("portal={Portal} body fetch timed out after {Timeout:0}s for {Url}",
                PortalName, EnrichFetchTimeout.TotalSeconds, url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            session.RecordFailure(url.Host);
            Logger.LogWarning("portal={Portal} body fetch failed for {Url}: {Reason}", PortalName, url, ex.Message);
            return null;
        }
    }

    private async Task<string?> FetchBodyHtmlWithTimeoutAsync(Uri url, CancellationToken ct)
    {
        using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        fetchCts.CancelAfter(EnrichFetchTimeout);
        return await FetchBodyHtmlAsync(url, fetchCts.Token);
    }

    private static readonly Regex JobindexAreaSpan = new(
        @"<span[^>]*\bclass\s*=\s*[""'][^""']*jix_robotjob--area[^""']*[""'][^>]*>([^<]+)</span>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    private static readonly Regex JobindexExternalLink = new(
        @"<a[^>]*\bclass\s*=\s*[""'][^""']*seejobdesktop[^""']*[""'][^>]*\bhref\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
}
