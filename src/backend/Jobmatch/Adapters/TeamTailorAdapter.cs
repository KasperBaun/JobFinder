using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

// TeamTailor career sites (e.g. karriere.danskespil.dk) don't expose a no-auth public
// jobs API — only the API at api.teamtailor.com which needs a per-tenant key. But every
// TeamTailor site publishes a sitemap.xml with /jobs/<id>-<slug> URLs and embeds a
// schema.org JobPosting JSON-LD blob in each job page. We harvest both: parse the
// sitemap for job URLs, fetch each page, extract the JSON-LD, map it to Listing.
//
// Endpoint: the sitemap URL (https://karriere.<tenant>.dk/sitemap.xml).
// Static fields supply company name (TeamTailor's hiringOrganization.name is also
// readable but staticFields lets the catalog override it).
public sealed class TeamTailorAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly Regex JobUrlPattern = new(@"/jobs/\d+", RegexOptions.Compiled);
    private static readonly Regex JsonLdScript = new(
        @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.+?)</script>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
            throw new ConfigException($"portal '{PortalName}': teamtailor adapter requires 'endpoint' (sitemap URL)");

        await ThrottleAsync(ct);
        var sitemapXml = await Http.GetStringAsync(Config.Endpoint, ct);

        XDocument doc;
        try { doc = XDocument.Parse(sitemapXml); }
        catch (Exception ex) { throw new ConfigException($"portal '{PortalName}': sitemap parse error — {ex.Message}", ex); }

        var jobUrls = doc.Descendants(SitemapNs + "url")
            .Select(u => u.Element(SitemapNs + "loc")?.Value)
            .Where(u => !string.IsNullOrWhiteSpace(u) && JobUrlPattern.IsMatch(u!))
            .Select(u => new Uri(u!))
            .Distinct()
            .ToList();

        var listings = new List<Listing>();
        for (var i = 0; i < jobUrls.Count; i++)
        {
            try
            {
                if (i > 0) await Task.Delay(BodyFetchDelayMs, ct);
                var listing = await FetchJobAsync(jobUrls[i], ct);
                if (listing is not null) listings.Add(listing);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "portal={Portal} skipped {Url}: {Message}", PortalName, jobUrls[i], ex.Message);
            }
        }
        return listings;
    }

    private async Task<Listing?> FetchJobAsync(Uri url, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        var html = await response.Content.ReadAsStringAsync(ct);

        var jobPosting = ExtractJobPostingJson(html);
        if (jobPosting is null)
        {
            Logger.LogWarning("portal={Portal} no JSON-LD JobPosting on {Url}", PortalName, url);
            return null;
        }

        var title = ReadString(jobPosting.Value, "title");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var descRaw = ReadString(jobPosting.Value, "description") ?? string.Empty;
        var description = StripHtml(System.Net.WebUtility.HtmlDecode(descRaw));

        var posted = ReadDate(jobPosting.Value, "datePosted");
        var (sourceId, hiringOrg) = ReadIdentifierAndOrg(jobPosting.Value);
        var location = ReadFirstJobLocation(jobPosting.Value);

        return BuildListing(
            sourceId: !string.IsNullOrWhiteSpace(sourceId) ? sourceId! : url.ToString(),
            title: title!.Trim(),
            company: hiringOrg,
            location: location,
            description: description,
            url: url,
            postedAt: posted,
            raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    // Pages can carry multiple JSON-LD scripts (BreadcrumbList, Organization, etc.).
    // We pick the first one whose @type is JobPosting — straightforward parse, no
    // graph-resolution since TeamTailor emits the JobPosting flat.
    private static JsonElement? ExtractJobPostingJson(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in JsonLdScript.Matches(html))
        {
            var raw = m.Groups[1].Value.Trim();
            JsonDocument doc;
            try { doc = JsonDocument.Parse(raw); }
            catch { continue; }
            var type = ReadString(doc.RootElement, "@type");
            if (string.Equals(type, "JobPosting", StringComparison.OrdinalIgnoreCase))
                return doc.RootElement.Clone();
            doc.Dispose();
        }
        return null;
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static DateTimeOffset? ReadDate(JsonElement el, string name)
    {
        var s = ReadString(el, name);
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
            ? dto
            : null;
    }

    private static (string? sourceId, string? hiringOrg) ReadIdentifierAndOrg(JsonElement el)
    {
        string? sourceId = null;
        if (el.TryGetProperty("identifier", out var ident) && ident.ValueKind == JsonValueKind.Object)
        {
            sourceId = ReadString(ident, "value");
        }
        string? org = null;
        if (el.TryGetProperty("hiringOrganization", out var ho) && ho.ValueKind == JsonValueKind.Object)
        {
            org = ReadString(ho, "name");
        }
        return (sourceId, org);
    }

    private static string? ReadFirstJobLocation(JsonElement el)
    {
        if (!el.TryGetProperty("jobLocation", out var jl)) return null;
        var first = jl.ValueKind == JsonValueKind.Array
            ? jl.EnumerateArray().FirstOrDefault()
            : jl;
        if (first.ValueKind != JsonValueKind.Object) return null;
        if (!first.TryGetProperty("address", out var addr) || addr.ValueKind != JsonValueKind.Object)
            return null;
        var locality = ReadString(addr, "addressLocality");
        var country = NormaliseCountry(ReadString(addr, "addressCountry"));
        if (!string.IsNullOrWhiteSpace(locality) && !string.IsNullOrWhiteSpace(country))
            return $"{locality}, {country}";
        return locality ?? country;
    }

    // TeamTailor stores addressCountry as the ISO-3166-1 alpha-2 code ("DK", "SE", "DE").
    // The ranker matches user-declared country names ("Denmark", "Sweden") via substring,
    // so the raw code doesn't match. Expand the few common European codes we'll see —
    // others pass through unchanged.
    private static string? NormaliseCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;
        return code.Trim().ToUpperInvariant() switch
        {
            "DK" => "Denmark",
            "SE" => "Sweden",
            "NO" => "Norway",
            "FI" => "Finland",
            "IS" => "Iceland",
            "DE" => "Germany",
            "NL" => "Netherlands",
            "GB" => "United Kingdom",
            "UK" => "United Kingdom",
            "IE" => "Ireland",
            "FR" => "France",
            "ES" => "Spain",
            "IT" => "Italy",
            "PL" => "Poland",
            "BE" => "Belgium",
            "AT" => "Austria",
            "CH" => "Switzerland",
            "US" => "United States",
            "CA" => "Canada",
            _ => code,
        };
    }
}
