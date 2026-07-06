using System.Text.Json;
using CodeHollow.FeedReader;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class RssAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
        {
            throw new ConfigException($"portal '{PortalName}': rss adapter requires 'endpoint'");
        }

        // Jobindex / it-jobbank feeds honor a `page` cursor; when Config.Pagination is set
        // FetchPagesAsync walks the pages (stopping at the first empty/duplicate page) so we
        // fetch the whole result set instead of just the ~20 most-recent items on page 1.
        // Enrichment runs once on the merged, de-duplicated set.
        var listings = await FetchPagesAsync(FetchFeedPageAsync, ct);

        if (Config.EnrichBody && listings.Count > 0)
        {
            return await EnrichBodiesAsync(listings, ct);
        }

        return listings;
    }

    private async Task<IReadOnlyList<Listing>> FetchFeedPageAsync(
        IReadOnlyDictionary<string, object?>? queryParams, CancellationToken ct)
    {
        await ThrottleAsync(ct);
        var feedUrl = AppendQueryParams(Config.Endpoint!, queryParams);
        // FeedReader.ReadAsync silently re-encodes the URL — confirmed empirically that
        // it strips literal `+` characters from the query string, breaking boolean AND
        // queries on Jobindex.dk (`?q=+.net+udvikler` arrives at the server as just
        // `.net udvikler`, which is an OR query). Fetch the bytes ourselves via the
        // HttpClient (encoding-preserving) and hand FeedReader the parsed string.
        var xml = await Http.GetStringAsync(feedUrl, ct);
        var feed = FeedReader.ReadFromString(xml);
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
                var (cleanTitle, extractedCompany) = ExtractJobindexTrailingCompany(title.Trim(), uri);

                listings.Add(BuildListing(
                    sourceId: string.IsNullOrWhiteSpace(item.Id) ? uri.ToString() : item.Id!,
                    title: cleanTitle,
                    company: extractedCompany,
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

        return listings;
    }

    // Jobindex / it-jobbank RSS titles consistently append ", <Company>" (often with
    // a legal form like " A/S" / " ApS"). Splitting that suffix into the company field
    // cleans up the displayed title and lets the deduper match against portals that
    // already populate Company correctly. Gated on host so unrelated RSS feeds aren't
    // accidentally re-shaped.
    internal static (string Title, string? Company) ExtractJobindexTrailingCompany(string title, Uri url)
    {
        if (!url.Host.Contains("jobindex.dk", StringComparison.OrdinalIgnoreCase)
            && !url.Host.Contains("it-jobbank.dk", StringComparison.OrdinalIgnoreCase))
            return (title, null);
        var idx = title.LastIndexOf(", ", StringComparison.Ordinal);
        if (idx <= 0 || idx >= title.Length - 2) return (title, null);
        var suffix = title[(idx + 2)..].Trim();
        var prefix = title[..idx].TrimEnd();
        if (suffix.Length == 0 || prefix.Length == 0) return (title, null);
        return (prefix, suffix);
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
