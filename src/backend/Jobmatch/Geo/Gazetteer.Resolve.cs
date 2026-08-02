using System.Text;
using System.Text.RegularExpressions;

namespace Jobmatch.Geo;

public sealed partial class Gazetteer
{
    // Mirrors the remote/worldwide handling in Ranker.Location.cs: such listings are not
    // place-bound, so they fall through the radius filter unresolved (never dropped).
    private static readonly string[] RemoteTokens = ["worldwide", "anywhere", "global", "remote"];

    private static readonly Regex DkPostalRegex = new(@"\b(\d{4})\b", RegexOptions.Compiled);

    private static readonly char[] SegmentSeparators = [',', '/', '·'];

    /// <summary>Best single match for a location string: most specific type wins across
    /// segments; within a bucket the home country wins, then the highest population.</summary>
    public GeoPlace? Resolve(string? location, string? homeCountryCode)
    {
        var places = ResolveAll(location, homeCountryCode);
        if (places.Count == 0) return null;
        var best = places[0];
        for (var i = 1; i < places.Count; i++)
        {
            if (places[i].Type < best.Type) best = places[i];
        }
        return best;
    }

    /// <summary>Every place the location's segments resolve to — multi-site listings
    /// ("Copenhagen or Aarhus") yield one entry per resolvable segment.</summary>
    public IReadOnlyList<GeoPlace> ResolveAll(string? location, string? homeCountryCode)
    {
        if (string.IsNullOrWhiteSpace(location)) return [];
        var lower = location.ToLowerInvariant();
        if (RemoteTokens.Any(t => lower.Contains(t, StringComparison.Ordinal))) return [];

        var places = new List<GeoPlace>();
        foreach (var segment in lower.Replace(" - ", ",").Split(SegmentSeparators))
        {
            var place = ResolveSegment(segment, homeCountryCode);
            if (place is not null && !places.Contains(place)) places.Add(place);
        }
        return places;
    }

    private GeoPlace? ResolveSegment(string segment, string? homeCountryCode)
    {
        var postal = DkPostalRegex.Match(segment);
        if (postal.Success && _byPostal.TryGetValue(postal.Value, out var postalEntry))
            return postalEntry.ToPlace();

        var exact = NormalizeKey(segment);
        if (exact.Length == 0) return null;
        foreach (var key in (string[])[exact, FoldDanish(exact), FoldPlain(exact)])
        {
            if (_byName.TryGetValue(key, out var bucket))
                return Pick(bucket, homeCountryCode).ToPlace();
        }
        return null;
    }

    // Buckets are pre-sorted by (specificity, population desc); prefer a home-country
    // entry within the most specific tier present.
    private static GazetteerEntry Pick(List<GazetteerEntry> bucket, string? homeCountryCode)
    {
        var best = bucket[0];
        if (homeCountryCode is null) return best;
        foreach (var entry in bucket)
        {
            if (entry.Type != best.Type) break;
            if (string.Equals(entry.CountryCode, homeCountryCode, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return best;
    }

    private static string NormalizeKey(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Danish-convention folding: å→aa, æ→ae, ø→oe — makes "århus" and "aarhus" meet.
    private static string FoldDanish(string s) => StripDiacritics(s
        .Replace("å", "aa").Replace("æ", "ae").Replace("ø", "oe")
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss"));

    // Plain folding: diacritics dropped entirely — makes "malmo" meet "malmö".
    private static string FoldPlain(string s) => StripDiacritics(s
        .Replace("å", "a").Replace("æ", "ae").Replace("ø", "o")
        .Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss"));

    private static string StripDiacritics(string s)
    {
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
