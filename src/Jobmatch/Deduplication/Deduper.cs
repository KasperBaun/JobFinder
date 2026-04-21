using System.Text.RegularExpressions;
using Jobmatch.Models;

namespace Jobmatch.Deduplication;

public static class Deduper
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<Listing> Deduplicate(IEnumerable<Listing> listings)
    {
        var seenUrls = new HashSet<string>(StringComparer.Ordinal);
        var seenTitleCompanyLocation = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Listing>();

        foreach (var listing in listings)
        {
            var urlKey = NormaliseUrl(listing.Url);
            if (!seenUrls.Add(urlKey)) continue;

            var tclKey = $"{Normalise(listing.Title)}|{Normalise(listing.Company ?? string.Empty)}|{Normalise(listing.Location ?? string.Empty)}";
            if (!seenTitleCompanyLocation.Add(tclKey)) continue;

            result.Add(listing);
        }

        return result;
    }

    internal static string NormaliseUrl(Uri url)
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
