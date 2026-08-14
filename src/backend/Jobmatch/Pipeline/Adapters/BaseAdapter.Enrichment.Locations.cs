using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Jobmatch.Pipeline.Adapters;

// Location repair during body enrichment. Some catalogs ship location strings that are known to
// be incomplete — Workday's list API literally says "2 Locations" for a multi-site posting, and
// a SuccessFactors list cell shows the first site plus a "+2 more…" affordance — which the
// ranking pipeline can neither score nor radius-filter honestly. Every repair runs on data an
// enrichment fetch pulls anyway: Workday's CXS detail JSON (every location + the clean
// description), the schema.org JobPosting JSON-LD block most ATS pages embed (primary location
// only), and the same posting marked up as inline microdata (every location).
public abstract partial class BaseAdapter
{
    private static readonly Regex PlaceholderLocation = new(
        @"^\s*\d+\s+locations?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Helsinki, FI, 00500 +1 more…" — the cell says outright that it names one of several sites.
    private static readonly Regex TruncationAffordance = new(
        @"\s*\+\s*\d+\s+more\s*(…|\.{3})?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static bool IsMissingOrPlaceholderLocation(string? location) =>
        string.IsNullOrWhiteSpace(location)
        || PlaceholderLocation.IsMatch(location)
        || TruncationAffordance.IsMatch(location);

    // The affordance is scraper chrome, not a place. Stripped after enrichment has had its chance
    // to recover the missing sites, so what is stored is either the full list or the one site the
    // list page did name.
    internal static string? StripTruncationAffordance(string? location)
    {
        if (location is null) return null;
        var stripped = TruncationAffordance.Replace(location, string.Empty).Trim();
        return stripped.Length == 0 ? null : stripped;
    }

    private static readonly Regex WorkdayJobUrl = new(
        @"^https://[a-z0-9-]+\.wd\d+\.myworkdayjobs\.com/(?:[a-z]{2,3}(?:-[A-Za-z0-9]{2,4})?/)?(?<site>[^/]+)(?<path>/job/.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // https://simcorp.wd3.myworkdayjobs.com/en-US/SimCorp_Jobs/job/Noida/Lead_R-1 →
    // https://simcorp.wd3.myworkdayjobs.com/wday/cxs/simcorp/SimCorp_Jobs/job/Noida/Lead_R-1
    // The tenant segment is the first host label; the locale segment is optional.
    internal static Uri? TryBuildWorkdayCxsUrl(Uri listingUrl)
    {
        var match = WorkdayJobUrl.Match(listingUrl.ToString());
        if (!match.Success) return null;
        var site = match.Groups["site"].Value;
        if (site.Equals("wday", StringComparison.OrdinalIgnoreCase)) return null;
        var tenant = listingUrl.Host.Split('.')[0];
        return new Uri($"https://{listingUrl.Host}/wday/cxs/{tenant}/{site}{match.Groups["path"].Value}");
    }

    internal sealed record WorkdayPosting(string? DescriptionHtml, IReadOnlyList<string> Locations, string? RemoteType);

    internal static WorkdayPosting? ParseWorkdayCxs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("jobPostingInfo", out var info)
                || info.ValueKind != JsonValueKind.Object)
                return null;
            var locations = new List<string>();
            AddDistinct(locations, StringOrNull(info, "location"));
            if (info.TryGetProperty("additionalLocations", out var extra) && extra.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in extra.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) AddDistinct(locations, item.GetString());
                }
            }
            return new WorkdayPosting(
                StringOrNull(info, "jobDescription"), locations, StringOrNull(info, "remoteType"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly Regex JsonLdBlock = new(
        @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(?<json>.+?)</script>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // "Locality, Country" per place, multiple places joined with " / " — comma and slash are both
    // segment separators the gazetteer splits on, so every place gets resolved individually.
    internal static string? ExtractJsonLdLocation(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        foreach (Match match in JsonLdBlock.Matches(html))
        {
            try
            {
                using var doc = JsonDocument.Parse(match.Groups["json"].Value);
                var location = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().Select(JobPostingLocation).FirstOrDefault(l => l is not null)
                    : JobPostingLocation(doc.RootElement);
                if (location is not null) return location;
            }
            catch (JsonException)
            {
                // Malformed or unrelated block — keep scanning.
            }
        }
        return null;
    }

    private static string? JobPostingLocation(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !string.Equals(StringOrNull(root, "@type"), "JobPosting", StringComparison.OrdinalIgnoreCase)
            || !root.TryGetProperty("jobLocation", out var jobLocation))
            return null;
        var places = new List<string>();
        if (jobLocation.ValueKind == JsonValueKind.Array)
        {
            foreach (var place in jobLocation.EnumerateArray()) AddDistinct(places, PlaceText(place));
        }
        else
        {
            AddDistinct(places, PlaceText(jobLocation));
        }
        return places.Count == 0 ? null : string.Join(" / ", places);
    }

    // The microdata sibling of ExtractJsonLdLocation. SuccessFactors career pages carry no JSON-LD;
    // they mark the posting up inline, with one address per site under itemprop="jobLocation" —
    // the only place the complete site list appears for a posting whose list cell was truncated.
    internal static string? ExtractMicrodataLocation(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)
            || !html.Contains("jobLocation", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var document = new HtmlParser().ParseDocument(html);
        var scopes = document.QuerySelectorAll("[itemprop='jobLocation'] [itemprop='address']");
        if (scopes.Length == 0) scopes = document.QuerySelectorAll("[itemprop='jobLocation']");

        var places = new List<string>();
        foreach (var scope in scopes) AddDistinct(places, MicrodataPlaceText(scope));
        return places.Count == 0 ? null : string.Join(" / ", places);
    }

    private static string? MicrodataPlaceText(IElement scope)
    {
        var locality = MicrodataValue(scope, "addressLocality");
        var country = MicrodataValue(scope, "addressCountry");
        if (locality is not null) return country is null ? locality : $"{locality}, {country}";
        return MicrodataValue(scope, "streetAddress") ?? country;
    }

    // A microdata value sits in the content attribute when the carrier is a <meta>, in the text
    // otherwise.
    private static string? MicrodataValue(IElement scope, string property)
    {
        var element = scope.QuerySelector($"[itemprop='{property}']");
        if (element is null) return null;
        var text = (element.GetAttribute("content") ?? element.TextContent).Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? PlaceText(JsonElement place)
    {
        if (place.ValueKind != JsonValueKind.Object
            || !place.TryGetProperty("address", out var address)
            || address.ValueKind != JsonValueKind.Object)
            return null;
        var locality = StringOrNull(address, "addressLocality");
        var country = StringOrNull(address, "addressCountry");
        return locality is not null && country is not null ? $"{locality}, {country}" : locality ?? country;
    }

    private static void AddDistinct(List<string> values, string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        if (!values.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) values.Add(trimmed);
    }

    private static string? StringOrNull(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
