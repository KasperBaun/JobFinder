using System.Text.Json;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Jobmatch.Adapters;

public sealed class HtmlAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
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

        try
        {
            return await FetchInternalAsync(ct);
        }
        catch (PlaywrightException ex) when (IsBrowserNotInstalled(ex))
        {
            Logger.LogWarning(
                "portal={Portal} Playwright browsers are not installed. Run: {Command}",
                PortalName, PlaywrightInstallCommand);
            return [];
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("playwright", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning(
                "portal={Portal} Playwright runtime missing. Run: {Command}",
                PortalName, PlaywrightInstallCommand);
            return [];
        }
    }

    public static string PlaywrightInstallCommand =>
        $"pwsh \"{Path.Combine(AppContext.BaseDirectory, "playwright.ps1")}\" install chromium";

    private static bool IsBrowserNotInstalled(PlaywrightException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("browser is not installed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("install browser", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<Listing>> FetchInternalAsync(CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Config.Endpoint!.ToString(), new PageGotoOptions { Timeout = 30000, WaitUntil = WaitUntilState.DOMContentLoaded });

        var html = Config.Html!;
        var cards = await page.QuerySelectorAllAsync(html.ListSelector);
        var results = new List<Listing>();

        foreach (var card in cards)
        {
            try
            {
                var title = await TextOf(card, html.TitleSelector);
                if (string.IsNullOrWhiteSpace(title)) continue;

                Uri? url = null;
                if (!string.IsNullOrWhiteSpace(html.LinkSelector))
                {
                    var link = await card.QuerySelectorAsync(html.LinkSelector);
                    if (link is not null)
                    {
                        var attr = await link.GetAttributeAsync(html.UrlAttribute ?? "href");
                        if (!string.IsNullOrWhiteSpace(attr))
                        {
                            var resolved = new Uri(Config.Endpoint!, attr);
                            if (resolved.IsAbsoluteUri) url = resolved;
                        }
                    }
                }
                if (url is null) continue;

                var company = html.CompanySelector is not null ? await TextOf(card, html.CompanySelector) : null;
                var location = html.LocationSelector is not null ? await TextOf(card, html.LocationSelector) : null;
                var description = html.DescriptionSelector is not null ? await TextOf(card, html.DescriptionSelector) : string.Empty;

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

    private static async Task<string?> TextOf(IElementHandle card, string selector)
    {
        var el = await card.QuerySelectorAsync(selector);
        if (el is null) return null;
        return await el.InnerTextAsync();
    }
}
