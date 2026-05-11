using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

// HR-Manager.net (candidate.hr-manager.net) is an ASP.NET WebForms ATS used by many DK
// employers — CHANGE of Scandinavia, DR Teknologi, and others. The /vacancies/list.aspx
// page server-side-renders a hidden form field 'HiddenField_PositionList' containing a
// JSON payload with every open position's title, location, dates and apply URL — no
// auth and no client-side rendering needed. Body text isn't included so this adapter
// enables EnrichBody automatically (each listing's AdvertisementUrl is fetched).
//
// Endpoint: https://candidate.hr-manager.net/vacancies/list.aspx?cid={cid}
public sealed class HrManagerAdapter(PortalConfig config, HttpClient http, ILogger logger) : BaseAdapter(config, http, logger)
{
    private static readonly Regex PositionListField = new(
        @"HiddenField_PositionList[^>]*\bvalue\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AspNetDate = new(@"^/Date\((-?\d+)\)/$", RegexOptions.Compiled);

    public override async Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default)
    {
        if (Config.Endpoint is null)
            throw new ConfigException($"portal '{PortalName}': hr-manager adapter requires 'endpoint' (e.g. /vacancies/list.aspx?cid={{cid}})");

        await ThrottleAsync(ct);
        var html = await Http.GetStringAsync(AppendQueryParams(Config.Endpoint, Config.QueryParams), ct);

        var match = PositionListField.Match(html);
        if (!match.Success)
        {
            Logger.LogWarning("portal={Portal}: HiddenField_PositionList not found on page (the layout may have changed)", PortalName);
            return [];
        }

        var encoded = match.Groups[1].Value;
        var decoded = System.Net.WebUtility.HtmlDecode(encoded);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(decoded); }
        catch (Exception ex)
        {
            throw new ConfigException($"portal '{PortalName}': position list JSON parse error — {ex.Message}", ex);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("PositionList", out var pl)
                || !pl.TryGetProperty("Items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                Logger.LogWarning("portal={Portal}: PositionList.Items missing or malformed", PortalName);
                return [];
            }

            var customer = ReadString(doc.RootElement, "CustomerName") ?? ReadString(doc.RootElement, "CustomerAlias");
            var listings = new List<Listing>();
            foreach (var item in items.EnumerateArray())
            {
                try { listings.Add(BuildFromPosition(item, customer)); }
                catch (Exception ex) { Logger.LogWarning(ex, "portal={Portal} skipped malformed position", PortalName); }
            }
            return Config.EnrichBody && listings.Count > 0
                ? await EnrichBodiesAsync(listings, ct)
                : listings;
        }
    }

    private Listing BuildFromPosition(JsonElement p, string? customerName)
    {
        var id = p.TryGetProperty("Id", out var idEl) ? idEl.GetRawText() : Guid.NewGuid().ToString("N");
        var title = ReadString(p, "Name") ?? string.Empty;
        var url = ReadString(p, "AdvertisementUrl") ?? ReadString(p, "AdvertisementUrlSecure") ?? string.Empty;
        // Location priority: DepartmentTree (root, has the geographic city/country),
        // then Department leaf (often empty for DR / Sundhed.dk-style nested orgs),
        // then WorkPlace (sometimes a building name like "DR Byen" — useful as
        // human-readable fallback but doesn't tier-match Copenhagen).
        var location = LocationFromDepartmentTree(p)
            ?? LocationFromDepartment(p)
            ?? ReadString(p, "WorkPlace");
        var posted = ReadAspNetDate(p, "Published");
        var company = customerName;
        if (p.TryGetProperty("Department", out var dept) && dept.ValueKind == JsonValueKind.Object)
        {
            // Department name often carries the actual hiring brand for multi-brand customers
            // (e.g. "Politiet — Center for CNE-Operationer"). Prefer it when present.
            var deptName = ReadString(dept, "Name");
            if (!string.IsNullOrWhiteSpace(deptName) && string.IsNullOrWhiteSpace(company))
                company = deptName;
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("position missing Name or AdvertisementUrl");

        return BuildListing(
            sourceId: id,
            title: title.Trim(),
            company: company,
            location: location,
            description: ReadString(p, "ShortDescription") ?? string.Empty,
            url: new Uri(url),
            postedAt: posted,
            raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    private static string? LocationFromDepartment(JsonElement p)
    {
        if (!p.TryGetProperty("Department", out var dept) || dept.ValueKind != JsonValueKind.Object)
            return null;
        var city = ReadString(dept, "City");
        var country = ReadString(dept, "Country");
        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
            return $"{city}, {country}";
        return city ?? country;
    }

    private static string? LocationFromDepartmentTree(JsonElement p)
    {
        if (!p.TryGetProperty("DepartmentTree", out var dept) || dept.ValueKind != JsonValueKind.Object)
            return null;
        var city = ReadString(dept, "City");
        var country = NormaliseCountry(ReadString(dept, "Country"));
        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
            return $"{city}, {country}";
        return city ?? country;
    }

    // HR-Manager serves Danish-language country values for DK customers ("Danmark",
    // "Norge", "Sverige"). The ranker matches user-declared country names ("Denmark")
    // via substring, so we need the English form for tier-matching to work.
    private static string? NormaliseCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return country;
        return country.Trim().ToLowerInvariant() switch
        {
            "danmark" => "Denmark",
            "norge" => "Norway",
            "sverige" => "Sweden",
            "finland" => "Finland",
            "island" => "Iceland",
            "tyskland" => "Germany",
            "holland" => "Netherlands",
            "storbritannien" => "United Kingdom",
            _ => country,
        };
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // HR-Manager serializes DateTimes as ASP.NET legacy '/Date(<ms-since-epoch>)/'.
    private static DateTimeOffset? ReadAspNetDate(JsonElement el, string name)
    {
        var s = ReadString(el, name);
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = AspNetDate.Match(s);
        if (!m.Success) return null;
        if (!long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return null;
        // Treat large negative ticks ("never set") as null instead of year 0001.
        if (ms < 0) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }
}
