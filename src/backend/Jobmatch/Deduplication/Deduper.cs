using System.Text.RegularExpressions;
using Jobmatch.Models;

namespace Jobmatch.Deduplication;

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

    public static DedupeResult Deduplicate(IEnumerable<Listing> listings)
    {
        var byUrl = new Dictionary<string, string>(StringComparer.Ordinal);
        var byTcl = new Dictionary<string, string>(StringComparer.Ordinal);
        var mergedInto = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var deduped = new List<Listing>();

        foreach (var listing in listings)
        {
            var urlKey = NormaliseUrl(listing.Url);
            if (byUrl.TryGetValue(urlKey, out var canonicalByUrl))
            {
                mergedInto[canonicalByUrl].Add(listing.Id);
                continue;
            }

            var tclKey = $"{Normalise(listing.Title)}|{NormaliseCompany(listing.Company)}|{NormaliseLocation(listing.Location)}";
            if (byTcl.TryGetValue(tclKey, out var canonicalByTcl))
            {
                mergedInto[canonicalByTcl].Add(listing.Id);
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
        var lowered = input.Trim().ToLowerInvariant();
        return WhitespaceRegex.Replace(lowered, " ");
    }

    internal static string NormaliseCompany(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var stripped = CompanyLegalFormSuffix.Replace(input.Trim(), string.Empty);
        return Normalise(stripped);
    }

    internal static string NormaliseLocation(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var t = LocationDanishRemoteSuffix.Replace(input.Trim(), string.Empty);
        var commaIdx = t.IndexOf(',');
        if (commaIdx > 0) t = t[..commaIdx];
        t = LocationDistrictSuffix.Replace(t, string.Empty);
        return Normalise(t);
    }
}
