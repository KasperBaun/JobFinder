using Jobmatch.Domain;

namespace Jobmatch.Pipeline.Ranking;

/// <summary>
/// Location and remote-work scoring, including the Danish/English city aliasing that lets a
/// listing for "København" match a user who wrote "Copenhagen" (R-090). Split from Ranker.cs to
/// keep both files inside the 300-line limit.
/// </summary>
public static partial class Ranker
{
    private static (double score, bool? locationMatch, bool? remoteMatch) ScoreLocationRemote(Listing listing, Skillset skillset, RankingConfig ranking)
    {
        bool? remoteMatch = ComputeRemoteMatch(listing.RemoteMode, skillset.RemotePreference);

        if (skillset.RemotePreference == RemotePreference.Any)
        {
            // No remote-mode preference — which never meant "anywhere on the globe". The signal
            // becomes location feasibility alone: fully remote listings are only checked against
            // the regional restriction, an undisclosed location stays neutral, and a far-away
            // office scores its tier so it can't tie with a listing in the user's city.
            var (tier, tierMatch) = LocationTier(listing.Location, skillset, ranking.LocationTierWeights);
            double anyScore;
            if (tier is null)
            {
                anyScore = 1.0;
            }
            else if (listing.RemoteMode == RemoteMode.Remote)
            {
                anyScore = tier.Value >= ranking.LocationTierWeights.Region ? 1.0 : tier.Value;
            }
            else
            {
                anyScore = tier.Value;
            }
            return (anyScore, tierMatch, remoteMatch);
        }

        // R: how compatible the listing's remote mode is with the user's preference.
        var R = (listing.RemoteMode, skillset.RemotePreference) switch
        {
            (RemoteMode.Remote, RemotePreference.Remote) => 1.0,
            (RemoteMode.Hybrid, RemotePreference.Hybrid) => 1.0,
            (RemoteMode.Onsite, RemotePreference.Onsite) => 1.0,
            (RemoteMode.Remote, RemotePreference.Hybrid) => 0.5,
            (RemoteMode.Hybrid, RemotePreference.Remote) => 0.5,
            (RemoteMode.Hybrid, RemotePreference.Onsite) => 0.5,
            (RemoteMode.Unknown, _) => 0.0,
            _ => 0.0,
        };

        // L: how feasible the listing's location is for the user (nullable when listing has no location).
        var (L, locationMatch) = LocationTier(listing.Location, skillset, ranking.LocationTierWeights);

        if (L is null)
        {
            // Listing didn't disclose a location — fall back to remote-mode compatibility alone.
            return (R, locationMatch, remoteMatch);
        }

        double score;
        if (listing.RemoteMode == RemoteMode.Unknown)
        {
            // Adapter couldn't tell remote/hybrid/onsite — fall back to location alone
            // rather than zeroing the signal.
            score = L.Value;
        }
        else if (listing.RemoteMode == RemoteMode.Remote)
        {
            // For remote listings, location acts as a regional restriction. If the listing
            // sits in the user's region or closer (L >= region tier), don't penalise; else
            // multiply by the tier so US-only remote roles get heavily discounted.
            var threshold = ranking.LocationTierWeights.Region;
            var effective = L.Value >= threshold ? 1.0 : L.Value;
            score = R * effective;
        }
        else
        {
            // For onsite/hybrid listings, the user must physically be there at some
            // cadence — multiply remote-mode compatibility by the location tier so a
            // city role beats a same-country role beats a foreign role.
            score = R * L.Value;
        }

        return (score, locationMatch, remoteMatch);
    }

    // Returns the tier weight for the listing location relative to the user, and a
    // boolean for "did this match the user's specific location" (city or metro tier).
    // Returns (null, null) when the listing has no location string.
    private static (double? tier, bool? locationMatch) LocationTier(string? listingLocation, Skillset skillset, LocationTierWeights w)
    {
        if (string.IsNullOrWhiteSpace(listingLocation)) return (null, null);
        var l = listingLocation.ToLowerInvariant();

        // Worldwide / global = top tier regardless of user.
        string[] global = ["worldwide", "anywhere", "global"];
        if (global.Any(t => l.Contains(t, StringComparison.Ordinal))) return (w.City, true);

        // City: substring match on the user's location string (last comma-piece treated as country, rest as city).
        // Expanded with known cross-language aliases so "København" in a Danish-language listing matches a user
        // who declared "Copenhagen" in English (and vice versa).
        var (userCity, derivedCountry) = SplitCityCountry(skillset.Location);
        if (!string.IsNullOrWhiteSpace(userCity) && AnyAliasMatches(l, userCity!))
            return (w.City, true);

        // Metro: any of the user's declared metro names (also alias-expanded).
        foreach (var m in skillset.Metro)
        {
            if (!string.IsNullOrWhiteSpace(m) && AnyAliasMatches(l, m))
                return (w.Metro, true);
        }

        // Country: explicit Country field, or derived from Location.
        var country = !string.IsNullOrWhiteSpace(skillset.Country) ? skillset.Country : derivedCountry;
        if (!string.IsNullOrWhiteSpace(country) && ContainsToken(l, country!.ToLowerInvariant()))
            return (w.Country, false);

        // Region: explicit Region field, with synonyms for the EU cluster.
        if (!string.IsNullOrWhiteSpace(skillset.Region))
        {
            var region = skillset.Region!.ToLowerInvariant();
            if (ContainsToken(l, region)) return (w.Region, false);
            if (region is "eu" or "europe" or "emea" or "eea")
            {
                string[] euSynonyms = ["europe", "european", "emea", "eea", "nordic", "scandinavia"];
                if (euSynonyms.Any(t => l.Contains(t, StringComparison.Ordinal))) return (w.Region, false);
                if (ContainsToken(l, "eu")) return (w.Region, false);
                if (EuMemberStates.Any(c => ContainsToken(l, c))) return (w.Region, false);
            }
        }

        return (w.Else, false);
    }

    // Cross-language aliases for cities the user might declare in one language and a listing
    // might use in another. Lowercase keys and values. Alphabetic order.
    // Symmetric: each name maps to the *other* names that should also match. Add new entries
    // sparingly — most cities don't need this and substring matching usually suffices.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CityAliases =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // Greater Copenhagen
            ["copenhagen"] = ["københavn", "kbh", "cph"],
            ["københavn"] = ["copenhagen", "kbh", "cph"],
            ["kbh"] = ["copenhagen", "københavn", "cph"],
            ["cph"] = ["copenhagen", "københavn", "kbh"],
            // "Greater Copenhagen" maps to the 14 suburb municipalities that make up
            // Statistics Denmark's Greater Copenhagen (plus the city itself). A listing
            // located in any of these earns the Metro tier when the user has
            // "Greater Copenhagen" or "Hovedstaden" in their metro list.
            ["greater copenhagen"] = [
                "storkøbenhavn", "københavn", "copenhagen",
                "brøndby", "albertslund", "ballerup", "dragør", "farum", "gentofte",
                "gladsaxe", "glostrup", "herlev", "hvidovre", "høje-taastrup",
                "ishøj", "lyngby-taarbæk", "rødovre", "tårnby", "vallensbæk",
            ],
            ["storkøbenhavn"] = [
                "greater copenhagen", "københavn", "copenhagen",
                "brøndby", "albertslund", "ballerup", "dragør", "farum", "gentofte",
                "gladsaxe", "glostrup", "herlev", "hvidovre", "høje-taastrup",
                "ishøj", "lyngby-taarbæk", "rødovre", "tårnby", "vallensbæk",
            ],
            ["hovedstaden"] = [
                "capital region", "københavn", "copenhagen",
                "brøndby", "albertslund", "ballerup", "dragør", "farum", "gentofte",
                "gladsaxe", "glostrup", "herlev", "hvidovre", "høje-taastrup",
                "ishøj", "lyngby-taarbæk", "rødovre", "tårnby", "vallensbæk",
            ],

            // Aarhus / Århus / Aalborg / Ålborg — DK uses both spellings interchangeably
            ["aarhus"] = ["århus"],
            ["århus"] = ["aarhus"],
            ["aalborg"] = ["ålborg"],
            ["ålborg"] = ["aalborg"],
            ["odense"] = [], // identical in EN/DA, here for completeness

            // Other DK metros that get hit
            ["helsingør"] = ["elsinore"],
            ["elsinore"] = ["helsingør"],
        };

    // True when the listing location contains the user's city name OR any known alias.
    // Substring + word-boundary check, same rules as ContainsToken.
    private static bool AnyAliasMatches(string lowerListingLocation, string userCity)
    {
        var lower = userCity.ToLowerInvariant();
        if (ContainsToken(lowerListingLocation, lower)) return true;
        if (!CityAliases.TryGetValue(lower, out var aliases)) return false;
        foreach (var a in aliases)
        {
            if (ContainsToken(lowerListingLocation, a)) return true;
        }
        return false;
    }

    // EU 27 + EEA non-EU (Iceland, Norway, Liechtenstein) + Switzerland.
    // UK is intentionally excluded post-Brexit; users who want UK can declare
    // Country: "United Kingdom" explicitly.
}
