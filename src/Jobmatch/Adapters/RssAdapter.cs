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
                    postedAt: item.PublishingDate is DateTime d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)) : null,
                    raw: JsonDocument.Parse("{}").RootElement.Clone()));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} skipped malformed feed item", PortalName);
            }
        }

        return listings;
    }
}
