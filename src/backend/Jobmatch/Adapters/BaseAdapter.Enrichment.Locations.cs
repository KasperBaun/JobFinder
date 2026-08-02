using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jobmatch.Adapters;

// Location repair during body enrichment. Some catalogs ship placeholder location strings —
// Workday's list API literally says "2 Locations" for a multi-site posting — which the ranking
// pipeline can neither score nor radius-filter. Both repairs run on data enrichment fetches
// anyway: Workday's CXS detail JSON (every location + the clean description) and the schema.org
// JobPosting JSON-LD block most ATS pages embed (primary location only).
public abstract partial class BaseAdapter
{
    private static readonly Regex PlaceholderLocation = new(
        @"^\s*\d+\s+locations?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static bool IsMissingOrPlaceholderLocation(string? location) =>
        string.IsNullOrWhiteSpace(location) || PlaceholderLocation.IsMatch(location);

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

    internal sealed record WorkdayPosting(string? DescriptionHtml, IReadOnlyList<string> Locations);

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
            return new WorkdayPosting(StringOrNull(info, "jobDescription"), locations);
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
