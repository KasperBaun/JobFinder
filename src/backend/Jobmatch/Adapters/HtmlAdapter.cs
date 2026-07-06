using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public sealed class HtmlAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    private static readonly HtmlParser Parser = new();

    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
        {
            throw new ConfigException($"portal '{PortalName}': html adapter requires 'endpoint'");
        }
        if (Config.Html is null)
        {
            throw new ConfigException($"portal '{PortalName}': html adapter requires 'html' selector block");
        }

        // Server-rendered career sites paginate via a query cursor (Nordea: `startrow` in
        // steps of 100). When Config.Pagination is set FetchPagesAsync walks the pages,
        // stopping at the first empty/duplicate page so JS-only lists (whose SSR fallback
        // repeats page 1) don't loop. Enrichment runs once on the merged set.
        var results = await FetchPagesAsync(FetchListPageAsync, ct);

        return Config.EnrichBody && results.Count > 0
            ? await EnrichBodiesAsync(results, ct)
            : results;
    }

    private async Task<IReadOnlyList<Listing>> FetchListPageAsync(
        IReadOnlyDictionary<string, object?>? queryParams, CancellationToken ct)
    {
        await ThrottleAsync(ct);

        using var req = new HttpRequestMessage(HttpMethod.Get, AppendQueryParams(Config.Endpoint!, queryParams));
        if (Config.Headers is { } headers)
        {
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        }
        // Most job-board servers reject the default .NET user-agent; spoof a modern desktop browser.
        if (!req.Headers.UserAgent.Any())
        {
            req.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        using var response = await Http.SendAsync(req, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var document = await Parser.ParseDocumentAsync(body, ct).ConfigureAwait(false);

        var html = Config.Html!;
        var cards = document.QuerySelectorAll(html.ListSelector);
        var results = new List<Listing>();

        foreach (var card in cards)
        {
            try
            {
                var title = TextOf(card, html.TitleSelector);
                if (string.IsNullOrWhiteSpace(title)) continue;

                Uri? url = null;
                if (!string.IsNullOrWhiteSpace(html.LinkSelector))
                {
                    // ":scope" means the list item itself is the <a> carrying the href.
                    // element.QuerySelector(":scope") returns null per DOM semantics (an element
                    // is not its own descendant), so resolve it to the card element explicitly.
                    var link = html.LinkSelector == ":scope" ? card : card.QuerySelector(html.LinkSelector);
                    var attr = link?.GetAttribute(html.UrlAttribute ?? "href");
                    if (!string.IsNullOrWhiteSpace(attr) &&
                        Uri.TryCreate(Config.Endpoint, attr, out var resolved) &&
                        resolved.IsAbsoluteUri)
                    {
                        url = resolved;
                    }
                }
                if (url is null) continue;

                var company = html.CompanySelector is not null ? TextOf(card, html.CompanySelector) : null;
                var location = html.LocationSelector is not null ? TextOf(card, html.LocationSelector) : null;
                var description = html.DescriptionSelector is not null ? TextOf(card, html.DescriptionSelector) : string.Empty;

                results.Add(BuildListing(
                    sourceId: url.ToString(),
                    title: title.Trim(),
                    company: string.IsNullOrWhiteSpace(company) ? null : company.Trim(),
                    location: string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
                    description: (description ?? string.Empty).Trim(),
                    url: url,
                    postedAt: null,
                    raw: JsonDocument.Parse("{}").RootElement.Clone()));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} skipped malformed card", PortalName);
            }
        }

        return results;
    }

    private static string? TextOf(IElement card, string selector)
    {
        var el = card.QuerySelector(selector);
        return el?.TextContent;
    }
}
