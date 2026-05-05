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

            var primaryHits = HitsOf(primaryRegexes, corpus);
            var secondaryHits = HitsOf(secondaryRegexes, corpus);
            var domainHits = HitsOf(domainRegexes, corpus);
            var disqualifierHits = HitsOf(disqualifierRegexes, corpus);

            var primaryFraction = Fraction(primaryHits.Count, skillset.PrimaryStack.Count);
            var secondaryFraction = Fraction(secondaryHits.Count, skillset.SecondaryStack.Count);
            var domainFraction = Fraction(domainHits.Count, skillset.Domains.Count);

            var (seniorityScore, seniorityMatch) = ScoreSeniority(listing, skillset.Seniority);
            var (locationRemoteScore, locationMatch, remoteMatch) = ScoreLocationRemote(listing, skillset);
            var freshnessScore = ScoreFreshness(listing.PostedAt, ranking.FreshnessHalfLifeDays);

            var w = ranking.Weights;
            var weighted = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["primary_stack"] = primaryFraction * w.PrimaryStack,
                ["secondary_stack"] = secondaryFraction * w.SecondaryStack,
                ["seniority"] = seniorityScore * w.Seniority,
                ["location_remote"] = locationRemoteScore * w.LocationRemote,
                ["domain"] = domainFraction * w.Domain,
                ["freshness"] = freshnessScore * w.Freshness,
            };

            var score = weighted.Values.Sum();
            if (disqualifierHits.Count > 0)
            {
                score *= ranking.DisqualifierPenalty;
            }
            score = Math.Clamp(score, 0.0, 1.0);

            var ageDays = AgeInDays(listing.PostedAt);
            var notes = BuildNotes(primaryHits, secondaryHits, domainHits, seniorityMatch, locationMatch, remoteMatch, disqualifierHits, listing, ageDays, ranking.FreshnessHalfLifeDays);

            matches.Add(new Match(
                Listing: listing,
                Score: score,
                Breakdown: weighted,
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
            .Where(m => m.Score >= ranking.MinScoreToInclude)
            .OrderByDescending(m => m.Score)
            .Take(ranking.TopN)
            .ToList();

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

    private static (double score, bool? match) ScoreSeniority(Listing listing, Seniority user)
    {
        var inferred = InferSeniority(listing.Title);
        if (user == Seniority.Any) return (1.0, true);
        if (inferred is null) return (0.5, null);
        if (inferred.Value == user) return (1.0, true);
        return IsAdjacent(inferred.Value, user) ? (0.5, true) : (0.0, false);
    }

    private static Seniority? InferSeniority(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        var lower = title.ToLowerInvariant();
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

    private static (double score, bool? locationMatch, bool? remoteMatch) ScoreLocationRemote(Listing listing, Skillset skillset)
    {
        bool? locationMatch = ComputeLocationMatch(listing.Location, skillset.Location);
        bool? remoteMatch = ComputeRemoteMatch(listing.RemoteMode, skillset.RemotePreference);

        if (skillset.RemotePreference == RemotePreference.Any)
        {
            return (1.0, locationMatch, remoteMatch);
        }

        var remoteScore = (listing.RemoteMode, skillset.RemotePreference) switch
        {
            (RemoteMode.Remote, RemotePreference.Remote) => 1.0,
            (RemoteMode.Hybrid, RemotePreference.Hybrid) => 1.0,
            (RemoteMode.Onsite, RemotePreference.Onsite) => locationMatch == true ? 1.0 : 0.5,
            (RemoteMode.Remote, RemotePreference.Hybrid) => 0.5,
            (RemoteMode.Hybrid, RemotePreference.Remote) => 0.5,
            (RemoteMode.Hybrid, RemotePreference.Onsite) => 0.5,
            (RemoteMode.Unknown, _) => 0.0,
            _ => 0.0,
        };

        double score;
        if (locationMatch == true)
        {
            score = 1.0;
        }
        else if (locationMatch is null)
        {
            // Listing has no location string — fall back to remote-mode compatibility.
            score = remoteScore;
        }
        else
        {
            // Listing has a location but it doesn't match the user. Discount by how
            // open the listing is to the user's region.
            score = remoteScore * LocationOpenness(listing.Location);
        }

        return (score, locationMatch, remoteMatch);
    }

    // Coarse heuristic: when a listing's location string doesn't match the user's,
    // is it nonetheless plausible the user could take the role? "Worldwide / EMEA /
    // Europe" → mostly yes (0.7). "USA only / LATAM" → mostly no (0.1). Anything
    // else (a single foreign city, an unfamiliar region) → ambiguous (0.3).
    private static double LocationOpenness(string? listingLocation)
    {
        if (string.IsNullOrWhiteSpace(listingLocation)) return 0.5;
        var l = listingLocation.ToLowerInvariant();

        string[] open = ["worldwide", "anywhere", "global", "europe", "european", "emea", " eu ", "eu/", "/eu"];
        if (open.Any(t => l.Contains(t, StringComparison.Ordinal))) return 0.7;

        string[] restrictive = ["usa", "u.s.", "united states", "americas", "latam", "brazil", "mexico", "argentina", "asia", "apac"];
        if (restrictive.Any(t => l.Contains(t, StringComparison.Ordinal))) return 0.1;

        return 0.3;
    }

    private static bool? ComputeLocationMatch(string? listingLocation, string userLocation)
    {
        if (string.IsNullOrWhiteSpace(listingLocation)) return null;
        if (string.IsNullOrWhiteSpace(userLocation)) return null;
        var l = listingLocation.ToLowerInvariant();
        var u = userLocation.ToLowerInvariant();
        if (l.Contains(u, StringComparison.Ordinal) || u.Contains(l, StringComparison.Ordinal)) return true;

        var userTokens = u.Split(new[] { ' ', ',', '/', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in userTokens)
        {
            if (token.Length >= 4 && l.Contains(token, StringComparison.Ordinal)) return true;
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
        bool? locationMatch,
        bool? remoteMatch,
        IReadOnlyList<string> disqualifierHits,
        Listing listing,
        double? ageDays,
        double halfLifeDays)
    {
        if (disqualifierHits.Count > 0)
        {
            return $"Disqualified by: {string.Join(", ", disqualifierHits)}.";
        }

        var parts = new List<string>();
        if (primaryHits.Count > 0)
        {
            parts.Add($"Primary stack hits: {string.Join(", ", primaryHits)}.");
        }
        else
        {
            parts.Add("No primary-stack keywords matched.");
        }

        if (secondaryHits.Count > 0)
        {
            parts.Add($"Secondary: {string.Join(", ", secondaryHits)}.");
        }
        if (domainHits.Count > 0)
        {
            parts.Add($"Domain: {string.Join(", ", domainHits)}.");
        }

        parts.Add(seniorityMatch switch
        {
            true => "Seniority fits.",
            false => "Seniority mismatch.",
            null => "Seniority not stated.",
        });

        var locPart = (locationMatch, remoteMatch) switch
        {
            (true, _) => $"Location match ({listing.Location}).",
            (_, true) => $"Remote-mode compatible ({listing.RemoteMode.ToString().ToLowerInvariant()}).",
            (false, false) => "Neither location nor remote preference matches.",
            (null, null) => "Location/remote details not stated.",
            (false, null) => "Location doesn't match; remote mode unknown.",
            (null, false) => "Location unknown; remote mode doesn't fit.",
        };
        parts.Add(locPart);

        if (ageDays is double age && age > 2 * halfLifeDays)
        {
            parts.Add($"Posted {(int)Math.Round(age)} days ago — freshness signal heavily decayed.");
        }

        return string.Join(" ", parts);
    }
}
