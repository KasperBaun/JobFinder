using System.Text.RegularExpressions;
using Jobmatch.Models;

namespace Jobmatch.Deduplication;

public static class Deduper
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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

            var tclKey = $"{Normalise(listing.Title)}|{Normalise(listing.Company ?? string.Empty)}|{Normalise(listing.Location ?? string.Empty)}";
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
}
