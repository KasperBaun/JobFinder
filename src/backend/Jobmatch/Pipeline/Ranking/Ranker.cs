using System.Text.RegularExpressions;
using Jobmatch.Domain;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Pipeline.Ranking;

public static partial class Ranker
{
    public static IReadOnlyList<Match> Rank(IEnumerable<Listing> listings, Skillset skillset, RankingConfig ranking) =>
        Filter(Score(listings, skillset, ranking), ranking);

    public static IReadOnlyList<Match> Score(IEnumerable<Listing> listings, Skillset skillset, RankingConfig ranking)
    {
        var primaryRegexes = CompileKeywords(skillset.PrimaryStack);
        var secondaryRegexes = CompileKeywords(skillset.SecondaryStack);
        var domainRegexes = CompileKeywords(skillset.Domains);
        var disqualifierRegexes = CompileKeywords(skillset.Disqualifiers);
        var preferredCompanyRegexes = CompileKeywords(skillset.PreferredCompanies);

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
            // Preferred employers match the company name only — a listing that merely
            // mentions a dream company in its description isn't a job *at* that company.
            var preferredCompanyHits = HitsOf(preferredCompanyRegexes, listing.Company ?? string.Empty);

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
            var afterPreferredBoost = preferredCompanyHits.Count > 0
                ? Math.Min(afterTitleGate * ranking.PreferredCompanyBoost, 1.0)
                : afterTitleGate;
            var preferredCompanyDelta = afterPreferredBoost - afterTitleGate;
            var score = Math.Clamp(afterPreferredBoost, 0.0, 1.0);

            var breakdown = new ScoreBreakdown(
                PrimaryStack: primaryContribution,
                SecondaryStack: secondaryContribution,
                Seniority: seniorityContribution,
                LocationRemote: locationContribution,
                Domain: domainContribution,
                Freshness: freshnessContribution,
                DisqualifierPenalty: disqualifierDelta,
                NonEngineeringTitlePenalty: nonEngineeringTitleDelta,
                PreferredCompanyBonus: preferredCompanyDelta);

            var ageDays = AgeInDays(listing.PostedAt);
            // Only mention the title gate in the notes when it actually changed the score —
            // matching the regex with a multiplier of 1.0 means the user opted out of the gate.
            var titleGateActive = nonEngineeringTitle && ranking.NonEngineeringTitleMultiplier < 1.0;
            var noteKeys = BuildNotes(primaryHits, secondaryHits, domainHits, seniorityMatch, seniorityIsAdjacent, locationMatch, remoteMatch, disqualifierHits, titleGateActive, listing, ageDays, ranking.FreshnessHalfLifeDays);

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
                    Notes: RenderEnglish(noteKeys),
                    NoteKeys: noteKeys)));
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

}
