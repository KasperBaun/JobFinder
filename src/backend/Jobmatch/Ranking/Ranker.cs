using System.Text.RegularExpressions;
using Jobmatch.Models;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Ranking;

public static class Ranker
{
    public static IReadOnlyList<Match> Rank(IEnumerable<Listing> listings, Skillset skillset, RankingConfig ranking) =>
        Filter(Score(listings, skillset, ranking), ranking);

    public static IReadOnlyList<Match> Score(IEnumerable<Listing> listings, Skillset skillset, RankingConfig ranking)
    {
        var primaryRegexes = CompileKeywords(skillset.PrimaryStack);
        var secondaryRegexes = CompileKeywords(skillset.SecondaryStack);
        var domainRegexes = CompileKeywords(skillset.Domains);
        var disqualifierRegexes = CompileKeywords(skillset.Disqualifiers);

        var matches = new List<Match>();
        foreach (var listing in listings)
        {
            var corpus = $"{listing.Title}\n{listing.Description}";
            // Disqualifiers match title + company only — description matching produced too
            // many false positives ("Lead a team of junior-to-senior engineers" zeroed real
            // senior roles; "no relocation required" matched the disqualifier "relocation
            // required"). Title catches role-intent terms (Junior, Intern, Trainee); company
            // catches employer/marketplace blacklists (Lemon.io). See R-041.
            var disqualifierCorpus = $"{listing.Title}\n{listing.Company ?? string.Empty}";

            var primaryHits = HitsOf(primaryRegexes, corpus);
            var secondaryHits = HitsOf(secondaryRegexes, corpus);
            var domainHits = HitsOf(domainRegexes, corpus);
            var disqualifierHits = HitsOf(disqualifierRegexes, disqualifierCorpus);

            var primaryFraction = Fraction(primaryHits.Count, skillset.PrimaryStack.Count);
            var secondaryFraction = Fraction(secondaryHits.Count, skillset.SecondaryStack.Count);
            var domainFraction = Fraction(domainHits.Count, skillset.Domains.Count);

            var (seniorityScore, seniorityMatch, seniorityIsAdjacent) =
                ScoreSeniority(listing, skillset.Seniority, ranking.SeniorityAdjacencyCredit);
            var (locationRemoteScore, locationMatch, remoteMatch) = ScoreLocationRemote(listing, skillset, ranking);
            var freshnessScore = ScoreFreshness(listing.PostedAt, ranking.FreshnessHalfLifeDays);
            var nonEngineeringTitle = IsNonEngineeringTitle(listing.Title);

            var w = ranking.Weights;
            var primaryContribution = primaryFraction * w.PrimaryStack;
            var secondaryContribution = secondaryFraction * w.SecondaryStack;
            var seniorityContribution = seniorityScore * w.Seniority;
            var locationContribution = locationRemoteScore * w.LocationRemote;
            var domainContribution = domainFraction * w.Domain;
            var freshnessContribution = freshnessScore * w.Freshness;

            var preBenchmark = primaryContribution + secondaryContribution + seniorityContribution
                + locationContribution + domainContribution + freshnessContribution;
            var afterDisqualifier = disqualifierHits.Count > 0
                ? preBenchmark * ranking.DisqualifierPenalty
                : preBenchmark;
            var disqualifierDelta = afterDisqualifier - preBenchmark;
            var afterTitleGate = nonEngineeringTitle
                ? afterDisqualifier * ranking.NonEngineeringTitleMultiplier
                : afterDisqualifier;
            var nonEngineeringTitleDelta = afterTitleGate - afterDisqualifier;
            var score = Math.Clamp(afterTitleGate, 0.0, 1.0);

            var breakdown = new ScoreBreakdown(
                PrimaryStack: primaryContribution,
                SecondaryStack: secondaryContribution,
                Seniority: seniorityContribution,
                LocationRemote: locationContribution,
                Domain: domainContribution,
                Freshness: freshnessContribution,
                DisqualifierPenalty: disqualifierDelta,
                NonEngineeringTitlePenalty: nonEngineeringTitleDelta);

            var ageDays = AgeInDays(listing.PostedAt);
            // Only mention the title gate in the notes when it actually changed the score —
            // matching the regex with a multiplier of 1.0 means the user opted out of the gate.
            var titleGateActive = nonEngineeringTitle && ranking.NonEngineeringTitleMultiplier < 1.0;
            var notes = BuildNotes(primaryHits, secondaryHits, domainHits, seniorityMatch, seniorityIsAdjacent, locationMatch, remoteMatch, disqualifierHits, titleGateActive, listing, ageDays, ranking.FreshnessHalfLifeDays);

            matches.Add(new Match(
                Listing: listing,
                Score: score,
                Breakdown: breakdown,
                Reasoning: new MatchReasoning(
                    PrimaryStackHits: primaryHits,
                    SecondaryStackHits: secondaryHits,
                    DomainHits: domainHits,
                    SeniorityMatch: seniorityMatch,
                    LocationMatch: locationMatch,
                    RemoteMatch: remoteMatch,
                    DisqualifierHits: disqualifierHits,
                    Notes: notes)));
        }

        return matches;
    }

    public static IReadOnlyList<Match> Filter(IReadOnlyList<Match> scored, RankingConfig ranking) =>
        scored
            .Where(m => !IsBeyondMaxAge(m.Listing, ranking.MaxAgeDays))
            .Where(m => !LacksRequiredPrimaryHit(m, ranking))
            .Where(m => m.Score >= ranking.MinScoreToInclude)
            .OrderByDescending(m => m.Score)
            .Take(ranking.TopN)
            .ToList();

    private static bool LacksRequiredPrimaryHit(Match match, RankingConfig ranking) =>
        ranking.RequirePrimaryStackHit && match.Reasoning.PrimaryStackHits.Count == 0;

    private static bool IsBeyondMaxAge(Listing listing, int? maxAgeDays)
    {
        if (maxAgeDays is null) return false;
        if (listing.PostedAt is null) return false;
        var age = (DateTimeOffset.UtcNow - listing.PostedAt.Value).TotalDays;
        return age > maxAgeDays.Value;
    }

    private static Dictionary<string, Regex> CompileKeywords(IReadOnlyList<string> keywords)
    {
        var dict = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        foreach (var kw in keywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            dict[kw] = new Regex(@"(?<![\w+#])" + Regex.Escape(kw) + @"(?![\w+#])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        return dict;
    }

    private static IReadOnlyList<string> HitsOf(Dictionary<string, Regex> regexes, string corpus)
    {
        var hits = new List<string>();
        foreach (var (kw, re) in regexes)
        {
            if (re.IsMatch(corpus)) hits.Add(kw);
        }
        return hits;
    }

    private static double Fraction(int hits, int total) => total == 0 ? 0.0 : (double)hits / total;

    private static (double score, bool? match, bool isAdjacent) ScoreSeniority(
        Listing listing, Seniority user, double adjacencyCredit)
    {
        var inferred = InferSeniority(listing.Title, listing.Description);
        if (user == Seniority.Any) return (1.0, true, false);
        if (inferred is null) return (0.5, null, false);
        if (inferred.Value == user) return (1.0, true, false);
        return IsAdjacent(inferred.Value, user)
            ? (adjacencyCredit, true, true)
            : (0.0, false, false);
    }

    // Title looks clearly non-engineering even when the description happens to mention
    // engineering keywords. The override pattern lets "Software Engineering Manager",
    // "QA Engineer", "DevOps Lead", etc. through unscathed — they're still engineering.
    private static readonly Regex EngineeringOverrideTitle = new(
        @"\b(engineer|engineering|developer|architect|programmer|coder|sre|devops)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NonEngineeringTitle = new(
        @"\b(" +
            // Product / project / account / sales — the manager titles that drag in C# / SQL incidentally
            @"product\s+(manager|owner|director|lead)|project\s+manager|" +
            @"technical\s+product\s+manager|" +
            @"account\s+(manager|executive|director|representative)|" +
            @"sales\s+(manager|representative|executive|director|lead|specialist)|" +
            // Marketing / growth / content / strategy
            @"marketing\s+(manager|specialist|lead|analyst|director|coordinator|operations)|" +
            @"growth\s+(lead|manager|specialist|hacker|director)|" +
            @"content\s+(operations|specialist|writer|strategist|manager)|" +
            @"copywriter|technical\s+writer|" +
            @"strategy\s+(lead|manager|director)|strategist|" +
            // Operations / business — generic Operations Manager but not DevOps/SecOps/SRE
            @"operations\s+(manager|specialist|analyst|coordinator|lead)|" +
            @"business\s+(analyst|consultant|partner|developer)|" +
            // Finance
            @"financial?\s+(analyst|controller|specialist|manager|director|advisor)|" +
            @"\b(controller|accountant|bookkeeper)\b|" +
            @"central\s+finance|" +
            // Standalone analyst roles (data/fraud/etc) — Engineer override still applies
            @"data\s+analyst|fraud\s+(analyst|detection)|" +
            // Customer-facing
            @"customer\s+(success|support|service|experience)|" +
            // People / recruiting
            @"recruit(er|ing|ment)|talent\s+acquisition|" +
            @"\bhr\b|human\s+resources|people\s+(operations|partner|manager|director)|" +
            // QA without Engineer (e.g. QA Manager / QA Lead / QA Analyst)
            @"qa\s+(manager|lead|analyst|director)|quality\s+(manager|lead|analyst|director)|" +
            // Misc
            @"executive\s+assistant|graphic\s+designer|compliance\s+officer|" +
            @"counsel|attorney|paralegal" +
        @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsNonEngineeringTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (EngineeringOverrideTitle.IsMatch(title)) return false;
        return NonEngineeringTitle.IsMatch(title);
    }

    private static Seniority? InferSeniority(string title, string? description)
    {
        var fromTitle = MatchSeniority(title);
        if (fromTitle is not null) return fromTitle;
        return MatchSeniority(description);
    }

    private static Seniority? MatchSeniority(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var lower = text.ToLowerInvariant();
        if (Regex.IsMatch(lower, @"\b(jr\.?|junior|graduate|intern)\b")) return Seniority.Junior;
        if (Regex.IsMatch(lower, @"\b(sr\.?|senior)\b")) return Seniority.Senior;
        if (Regex.IsMatch(lower, @"\b(lead|principal|staff)\b")) return Seniority.Lead;
        if (Regex.IsMatch(lower, @"\b(mid|mid-level|intermediate)\b")) return Seniority.Mid;
        return null;
    }

    private static bool IsAdjacent(Seniority a, Seniority b)
    {
        if ((a == Seniority.Junior && b == Seniority.Mid) || (a == Seniority.Mid && b == Seniority.Junior)) return true;
        if ((a == Seniority.Mid && b == Seniority.Senior) || (a == Seniority.Senior && b == Seniority.Mid)) return true;
        if ((a == Seniority.Senior && b == Seniority.Lead) || (a == Seniority.Lead && b == Seniority.Senior)) return true;
        return false;
    }

    private static (double score, bool? locationMatch, bool? remoteMatch) ScoreLocationRemote(Listing listing, Skillset skillset, RankingConfig ranking)
    {
        bool? remoteMatch = ComputeRemoteMatch(listing.RemoteMode, skillset.RemotePreference);

        if (skillset.RemotePreference == RemotePreference.Any)
        {
            return (1.0, locationMatch: null, remoteMatch);
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
    private static readonly string[] EuMemberStates = [
        "austria", "belgium", "bulgaria", "croatia", "cyprus",
        "czech republic", "czechia", "denmark", "estonia", "finland",
        "france", "germany", "greece", "hungary", "iceland",
        "ireland", "italy", "latvia", "liechtenstein", "lithuania",
        "luxembourg", "malta", "netherlands", "norway", "poland",
        "portugal", "romania", "slovakia", "slovenia", "spain",
        "sweden", "switzerland",
    ];

    private static (string? city, string? country) SplitCityCountry(string? userLocation)
    {
        if (string.IsNullOrWhiteSpace(userLocation)) return (null, null);
        var parts = userLocation.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], parts[^1]),
        };
    }

    // Substring match with word boundaries on either side (anything that isn't a letter/digit).
    private static bool ContainsToken(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return false;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            var beforeOk = idx == 0 || !char.IsLetterOrDigit(haystack[idx - 1]);
            var afterIdx = idx + needle.Length;
            var afterOk = afterIdx == haystack.Length || !char.IsLetterOrDigit(haystack[afterIdx]);
            if (beforeOk && afterOk) return true;
            idx = afterIdx;
        }
        return false;
    }

    private static bool? ComputeRemoteMatch(RemoteMode listingMode, RemotePreference userPref)
    {
        if (listingMode == RemoteMode.Unknown) return null;
        if (userPref == RemotePreference.Any) return true;
        return (listingMode, userPref) switch
        {
            (RemoteMode.Remote, RemotePreference.Remote) => true,
            (RemoteMode.Hybrid, RemotePreference.Hybrid) => true,
            (RemoteMode.Onsite, RemotePreference.Onsite) => true,
            _ => false,
        };
    }

    private static double ScoreFreshness(DateTimeOffset? postedAt, double halfLifeDays)
    {
        if (postedAt is null) return 0.5;
        var age = (DateTimeOffset.UtcNow - postedAt.Value).TotalDays;
        if (age < 0) age = 0;
        return Math.Exp(-age / Math.Max(0.01, halfLifeDays));
    }

    private static double? AgeInDays(DateTimeOffset? postedAt)
    {
        if (postedAt is null) return null;
        var age = (DateTimeOffset.UtcNow - postedAt.Value).TotalDays;
        return age < 0 ? 0 : age;
    }

    private static string BuildNotes(
        IReadOnlyList<string> primaryHits,
        IReadOnlyList<string> secondaryHits,
        IReadOnlyList<string> domainHits,
        bool? seniorityMatch,
        bool seniorityIsAdjacent,
        bool? locationMatch,
        bool? remoteMatch,
        IReadOnlyList<string> disqualifierHits,
        bool nonEngineeringTitle,
        Listing listing,
        double? ageDays,
        double halfLifeDays)
    {
        if (disqualifierHits.Count > 0)
        {
            return $"Removed because of: {string.Join(", ", disqualifierHits)}.";
        }

        var parts = new List<string>();
        if (primaryHits.Count > 0)
        {
            parts.Add($"Must-have skill match: {string.Join(", ", primaryHits)}.");
        }
        else
        {
            parts.Add("None of your must-have skills mentioned.");
        }

        if (secondaryHits.Count > 0)
        {
            parts.Add($"Also matches: {string.Join(", ", secondaryHits)}.");
        }
        if (domainHits.Count > 0)
        {
            parts.Add($"Industry: {string.Join(", ", domainHits)}.");
        }

        parts.Add((seniorityMatch, seniorityIsAdjacent) switch
        {
            (true, true) => "Experience level close fit.",
            (true, false) => "Experience level matches.",
            (false, _) => "Experience level doesn't match.",
            (null, _) => "Experience level not stated.",
        });

        if (nonEngineeringTitle)
        {
            parts.Add("Title doesn't look like a developer role — rating reduced.");
        }

        var locPart = (locationMatch, remoteMatch) switch
        {
            (true, _) => $"Location: {listing.Location}.",
            (_, true) => $"Remote work OK ({listing.RemoteMode.ToString().ToLowerInvariant()}).",
            (false, false) => "Neither location nor remote setup matches.",
            (null, null) => "Location and remote setup not stated.",
            (false, null) => "Location doesn't match; remote setup not stated.",
            (null, false) => "Location not stated; remote setup doesn't match.",
        };
        parts.Add(locPart);

        if (ageDays is double age && age > 2 * halfLifeDays)
        {
            parts.Add($"Posted {(int)Math.Round(age)} days ago — rating reduced for age.");
        }

        return string.Join(" ", parts);
    }
}
