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

        await ThrottleAsync(ct);
        var feedUrl = AppendQueryParams(Config.Endpoint, Config.QueryParams);
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
