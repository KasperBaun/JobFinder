using System.Text.RegularExpressions;
using Jobmatch.Domain;

namespace Jobmatch.Search.Ranking;

// How a listing's seniority is read and compared to the user's: the title/description inference,
// the adjacency rule that gives a neighbouring level half credit, and the non-engineering-title
// guard that keeps a "Sales Manager" from scoring as a senior engineering role.
public static partial class Ranker
{
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
}
