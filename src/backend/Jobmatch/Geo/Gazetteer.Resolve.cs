using System.Text;
using System.Text.RegularExpressions;

namespace Jobmatch.Geo;

public sealed partial class Gazetteer
{
    private const string DanishCountryCode = "DK";

    private const int MinSplitPartLength = 3;

    // Mirrors the remote/worldwide handling in Ranker.Location.cs: such listings are not
    // place-bound, so they fall through the radius filter unresolved (never dropped).
    private static readonly string[] RemoteTokens = ["worldwide", "anywhere", "global", "remote"];

    // The postal layer is Danish-only, so a four-digit run counts only where it stands as its own
    // token: inside "23510-3300" or "2770-131" it is part of a foreign postcode, and in "H2020" it
    // is part of a name. The `DK-` prefix of the canonical Danish form ("DK-2800 Kgs. Lyngby") is
    // the one hyphen that must still pass, hence the narrower `\d-` lookbehind.
    private static readonly Regex DkPostalRegex =
        new(@"(?<![\p{L}\d])(?<!\d-)(\d{4})(?![\p{L}\d-])", RegexOptions.Compiled);

    private static readonly char[] SegmentSeparators = [',', '/', '·'];

    // Danish and English ads list several sites in one breath ("Glostrup & København",
    // "Roskilde og mulighed for hjemmearbejde", "Copenhagen | Aarhus"). Applied only after the
    // whole segment fails to resolve, so hyphenated and multi-word names — "Aix-en-Provence",
    // "Trinidad and Tobago" — survive intact.
    private static readonly Regex ConjunctionRegex =
        new(@"\s+(?:og|eller|and|or|med)\s+|\s*[;&+|–—-]\s*", RegexOptions.Compiled);

    // ATS location fields qualify the site in brackets — "Copenhagen (Primary)", "Aarhus [HQ]".
    // Tried only after the bracketed form itself misses, so a name that *contains* brackets
    // ("Halle (Saale)") still matches on its own terms.
    private static readonly Regex BracketedQualifierRegex = new(@"[(\[][^)\]]*[)\]]", RegexOptions.Compiled);

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

    /// <summary>The places a listing could actually sit at, at the finest granularity the string
    /// offers: a precise match (postal code or city) outranks a region, which outranks a country
    /// — "Aarhus, Denmark" is one site in Jutland, not a choice between Aarhus and Copenhagen.
    /// Postal and city rank together so a multi-site string ("Copenhagen, Aarhus or Aalborg")
    /// keeps every site even when they resolve through different layers.</summary>
    public IReadOnlyList<GeoPlace> ResolveSites(string? location, string? homeCountryCode)
    {
        var places = ResolveAll(location, homeCountryCode);
        if (places.Count < 2) return places;
        var precise = places
            .Where(p => p.Type is GeoPlaceType.Postal or GeoPlaceType.City)
            .ToList();
        if (precise.Count > 0) return precise;
        var regions = places.Where(p => p.Type is GeoPlaceType.Region).ToList();
        return regions.Count > 0 ? regions : places;
    }

    /// <summary>Every place the location resolves to — a multi-site listing ("Copenhagen or
    /// Aarhus") yields one entry per site, whether the sites are separated by punctuation or
    /// by a conjunction inside one segment.</summary>
    public IReadOnlyList<GeoPlace> ResolveAll(string? location, string? homeCountryCode)
    {
        if (string.IsNullOrWhiteSpace(location)) return [];
        var lower = location.ToLowerInvariant();
        if (RemoteTokens.Any(t => lower.Contains(t, StringComparison.Ordinal))) return [];

        var segments = lower.Replace(" - ", ",").Split(SegmentSeparators);
        var parsed = new List<(List<GeoPlace> Names, GazetteerEntry? Postal)>(segments.Length);
        foreach (var segment in segments)
        {
            parsed.Add((ResolveNames(segment, homeCountryCode), MatchPostal(segment)));
        }

        // The postal layer is Danish-only, so a four-digit code is believable only while nothing
        // foreign resolved anywhere in the string: "Philippines, Pasig, 1600" is Manila, and
        // "Antwerpen 2000" is Belgium — not København V.
        var foreignFound = parsed.Any(p => p.Names.Any(n => n.CountryCode != DanishCountryCode));

        var places = new List<GeoPlace>();
        foreach (var (names, postal) in parsed)
        {
            // Where both agree the segment is Danish, the code wins: it pins one district row,
            // while the district *name* is shared by every postcode in that district.
            if (postal is not null && !foreignFound)
                Add(places, postal.ToPlace());
            else
                foreach (var name in names) Add(places, name);
        }
        return places;
    }

    private static void Add(List<GeoPlace> places, GeoPlace place)
    {
        if (!places.Contains(place)) places.Add(place);
    }

    // Postal digits are stripped before the name lookup so "Wien 1010" reads as Vienna; the code
    // itself is read separately by MatchPostal.
    private List<GeoPlace> ResolveNames(string segment, string? homeCountryCode)
    {
        var text = DkPostalRegex.Replace(segment, " ");
        if (LookupName(text, homeCountryCode) is GeoPlace whole) return [whole];

        var parts = ConjunctionRegex.Split(text);
        if (parts.Length < 2) return [];
        var found = new List<GeoPlace>();
        foreach (var part in parts)
        {
            // Fragments this short are the two-letter country codes the index also answers to:
            // splitting "Île-de-France" or "Gyeonggi-do" must not surface Germany or the
            // Dominican Republic. A whole segment may still be "DK" — only pieces are suspect.
            if (NormalizeKey(part).Length < MinSplitPartLength) continue;
            if (LookupName(part, homeCountryCode) is GeoPlace place) Add(found, place);
        }
        return found;
    }

    private GeoPlace? LookupName(string text, string? homeCountryCode)
    {
        // Bracket handling runs before normalisation: NormalizeKey trims edge punctuation, which
        // would eat the closing bracket of "København (hovedkontor)" and hide the qualifier.
        foreach (var candidate in BracketCandidates(text))
        {
            var normalized = NormalizeKey(candidate);
            if (normalized.Length == 0) continue;
            foreach (var key in (string[])[normalized, FoldDanish(normalized), FoldPlain(normalized)])
            {
                if (_byName.TryGetValue(key, out var bucket))
                    return Pick(bucket, homeCountryCode).ToPlace();
            }
        }
        return null;
    }

    // "Copenhagen (Primary)" means Copenhagen; "[Copenhagen]" also means Copenhagen. The bracketed
    // form is tried first so a name that carries brackets ("Halle (Saale)") still wins on its own.
    private static string[] BracketCandidates(string raw)
    {
        if (raw.IndexOfAny(['(', '[']) < 0) return [raw];
        return
        [
            raw,
            BracketedQualifierRegex.Replace(raw, " "),
            raw.Replace("(", " ").Replace(")", " ").Replace("[", " ").Replace("]", " "),
        ];
    }

    private GazetteerEntry? MatchPostal(string segment)
    {
        var match = DkPostalRegex.Match(segment);
        return match.Success && _byPostal.TryGetValue(match.Value, out var entry) ? entry : null;
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

    // Any run of whitespace collapses to one space — scraped fields carry non-breaking spaces
    // between the words of a name as readily as plain ones.
    private static string NormalizeKey(string raw)
    {
        var cleaned = string.Concat(raw.ToLowerInvariant().Where(c => !IsZeroWidth(c)));
        var collapsed = string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        // Edge punctuation is decoration, never part of a name: "Copenhagen:" is Copenhagen.
        var start = 0;
        var end = collapsed.Length - 1;
        while (start <= end && !char.IsLetterOrDigit(collapsed[start])) start++;
        while (end >= start && !char.IsLetterOrDigit(collapsed[end])) end--;
        return collapsed[start..(end + 1)];
    }

    // Zero-width joiners and BOMs ride along in scraped location fields and are not whitespace,
    // so nothing else strips them.
    private static bool IsZeroWidth(char c) => c is '\u200B' or '\u200C' or '\u200D' or '\uFEFF';

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
