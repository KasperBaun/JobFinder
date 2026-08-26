using System.Net;
using System.Text.RegularExpressions;
using Jobmatch.Domain;
using Jobmatch.Domain.Runs;
using Jobmatch.Search.Locations;

namespace Jobmatch.Search.Deduplication;

public static class Deduper
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // "Sopra Steria A/S" vs "Sopra Steria" — same employer, different portal conventions.
    private static readonly Regex CompanyLegalFormSuffix = new(
        @"\s*,?\s+(A/S|ApS|AS|IVS|K/S|P/S|GmbH|AG|SARL|SAS|SA|NV|BV|Ltd|LLC|Inc|Corp|Oy|AB|Plc)\.?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Jobindex bakes the remote-mode hint into the location string itself, eg
    // "Brøndby og mulighed for hjemmearbejde". Strip it so the location key matches
    // the bare "Brøndby" / "Brøndby, Denmark" from other portals.
    private static readonly Regex LocationDanishRemoteSuffix = new(
        @"\s+og\s+mulighed\s+for\s+(hjemmearbejde|fjernarbejde)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Copenhagen postal-district letters ("København K", "København Ø", "København NV").
    // After taking the first comma segment we may still have "København K"; strip the
    // trailing 1-2 uppercase letters so it matches the bare city used by other portals.
    private static readonly Regex LocationDistrictSuffix = new(
        @"\s+\p{Lu}{1,2}\s*$",
        RegexOptions.Compiled);

    // Same employer, different upstream conventions. Keys are the *non-canonical*
    // forms (lowercased after legal-form strip + whitespace collapse); values are
    // the canonical form they collapse to. Add sparingly — most companies don't
    // need this and substring matching is too aggressive a default for dedupe.
    private static readonly IReadOnlyDictionary<string, string> CompanyCanonicalForm =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Jobindex extracts "DR" from the trailing title suffix; the hr-manager-dr
            // adapter sets "Danmarks Radio" from the upstream catalog. Same employer.
            ["dr"] = "danmarks radio",
        };

    public static DedupeResult Deduplicate(IEnumerable<Listing> listings, Gazetteer? gazetteer = null)
    {
        var byUrl = new Dictionary<string, string>(StringComparer.Ordinal);
        var byTcl = new Dictionary<string, string>(StringComparer.Ordinal);
        var mergedInto = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var deduped = new List<Listing>();

        foreach (var listing in listings)
        {
            var urlKey = NormaliseUrl(listing.Url);
            var tclKey = $"{Normalise(listing.Title)}|{NormaliseCompany(listing.Company)}|{NormaliseLocation(listing.Location, gazetteer)}";

            // An absorbed listing still registers its other key: portal B's spelling of an ad
            // already collapsed by URL would otherwise stay invisible to portal C's copy.
            if (byUrl.TryGetValue(urlKey, out var canonicalByUrl))
            {
                mergedInto[canonicalByUrl].Add(listing.Id);
                byTcl.TryAdd(tclKey, canonicalByUrl);
                continue;
            }

            if (byTcl.TryGetValue(tclKey, out var canonicalByTcl))
            {
                mergedInto[canonicalByTcl].Add(listing.Id);
                byUrl.TryAdd(urlKey, canonicalByTcl);
                continue;
            }

            byUrl[urlKey] = listing.Id;
            byTcl[tclKey] = listing.Id;
            mergedInto[listing.Id] = [];
            deduped.Add(listing);
        }

        var merges = mergedInto
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => new DedupeGroup(kvp.Key, kvp.Value))
            .ToList();

        return new DedupeResult(deduped, merges);
    }

    public static string NormaliseUrl(Uri url)
    {
        var builder = new UriBuilder(url)
        {
            Fragment = string.Empty,
        };
        var path = builder.Path.TrimEnd('/');
        if (string.IsNullOrEmpty(path)) path = "/";
        builder.Path = path;
        return builder.Uri.ToString().ToLowerInvariant();
    }

    internal static string Normalise(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        // Runs before ListingTextDecoder existed for older payloads, and portals disagree on
        // encoding depth ("&amp;amp;" vs "&amp;" vs "&"), so decode until stable.
        var decoded = input;
        while (decoded.Contains('&'))
        {
            var next = WebUtility.HtmlDecode(decoded);
            if (next == decoded) break;
            decoded = next;
        }
        var lowered = decoded.Trim().ToLowerInvariant();
        return WhitespaceRegex.Replace(lowered, " ");
    }

    internal static string NormaliseCompany(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var stripped = CompanyLegalFormSuffix.Replace(input.Trim(), string.Empty);
        var normalised = Normalise(stripped);
        return CompanyCanonicalForm.TryGetValue(normalised, out var canonical)
            ? canonical
            : normalised;
    }

    internal static string NormaliseLocation(string? input, Gazetteer? gazetteer = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var t = LocationDanishRemoteSuffix.Replace(input.Trim(), string.Empty);

        // A multi-site string must never share a key with one of its sites: "Aalborg, Denmark;
        // Aarhus, …" is a different posting than "Aalborg, Denmark", and the first-segment cut
        // below would reduce both to the same city (caught live in the T-013 run-6 audit). The
        // full string is resolved first; only a single-site string takes the reduction path.
        var fullSites = gazetteer?.ResolveSites(t, null);
        if (fullSites is { Count: > 1 }) return SiteSetKey(fullSites);

        var commaIdx = t.IndexOf(',');
        if (commaIdx > 0) t = t[..commaIdx];
        t = LocationDistrictSuffix.Replace(t, string.Empty);

        // "Copenhagen V, Denmark" (oracle) and "København" (jobindex) are the same place spelled
        // in two languages — string normalisation can never make them meet, so the reduced city
        // string is resolved through the gazetteer and the canonical places become the key. The
        // key is the *sorted set* of resolved sites: "Noida / Hyderabad" must match
        // "Hyderabad / Noida" but never a bare "Noida" — a destructive merge cannot ride on
        // whichever site a portal happened to list first. The '#' suffix keeps a resolved key
        // from ever colliding with an unresolved raw string.
        var sites = gazetteer?.ResolveSites(t, null);
        if (sites is { Count: > 0 }) return SiteSetKey(sites);

        return Normalise(t);
    }

    private static string SiteSetKey(IReadOnlyList<GeoPlace> sites) =>
        string.Join("+", sites
            .Select(p => $"{Normalise(p.Name)} #{p.CountryCode.ToLowerInvariant()}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
}
