using System.Text.Json;
using CodeHollow.FeedReader;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class RssAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    // Delay between body-enrichment fetches when EnrichBody is on. ~5rps so a 100-item
    // feed takes ~20s of body fetching, which the user sees as the "ranking" phase
    // taking longer. Sequential, not concurrent — keeps a single host (it-jobbank.dk
    // etc.) from getting hit too hard while still being faster than the feed's own
    // RateLimitRps (which is set very low to avoid rate-limit on the feed endpoint).
    private const int BodyFetchDelayMs = 200;

    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
        {
            throw new ConfigException($"portal '{PortalName}': rss adapter requires 'endpoint'");
        }

        await ThrottleAsync(ct);
        var feed = await FeedReader.ReadAsync(Config.Endpoint.ToString(), cancellationToken: ct);
        var listings = new List<Listing>();

        foreach (var item in feed.Items)
        {
            try
            {
                var link = item.Link;
                if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)) continue;

                var title = item.Title ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title)) continue;

                var description = StripHtml(item.Content ?? item.Description ?? string.Empty);

                listings.Add(BuildListing(
                    sourceId: string.IsNullOrWhiteSpace(item.Id) ? uri.ToString() : item.Id!,
                    title: title.Trim(),
                    company: null,
                    location: null,
                    description: description,
                    url: uri,
                    postedAt: ToUtc(item.PublishingDate),
                    raw: JsonDocument.Parse("{}").RootElement.Clone()));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} skipped malformed feed item", PortalName);
            }
        }

        if (Config.EnrichBody && listings.Count > 0)
        {
            return await EnrichBodiesAsync(listings, ct);
        }

        return listings;
    }

    // Fetches the linked HTML page for each listing and merges its visible text into
    // Description. Sequential with a small delay so we don't hammer the host. Failures
    // are logged but don't drop the listing — we keep the RSS-only version.
    internal async Task<IReadOnlyList<Listing>> EnrichBodiesAsync(IReadOnlyList<Listing> listings, CancellationToken ct)
    {
        var enriched = new List<Listing>(listings.Count);
        for (var i = 0; i < listings.Count; i++)
        {
            var listing = listings[i];
            try
            {
                if (i > 0) await Task.Delay(BodyFetchDelayMs, ct);
                var bodyHtml = await FetchBodyHtmlAsync(listing.Url, ct);
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

    private static DateTimeOffset? ToUtc(DateTime? dt)
    {
        if (dt is null) return null;
        var v = dt.Value;
        return v.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(v, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(v.ToUniversalTime(), TimeSpan.Zero),
            _ => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc), TimeSpan.Zero),
        };
    }
}
