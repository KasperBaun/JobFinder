using System.Text.RegularExpressions;
using Jobmatch.Domain;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace Jobmatch.Pipeline.Adapters;

/// <summary>
/// Remote-mode inference. Split from BaseAdapter.cs so the classifier and its pattern tables
/// live on their own; the entry point is <see cref="BaseAdapter.InferRemoteMode"/>.
/// </summary>
public abstract partial class BaseAdapter
{
    // Title and location are terse, deliberate signals: "Support Manager - Remote", "Aarhus C,
    // hybrid position", "København Ø og mulighed for hjemmearbejde". A bare token there is the
    // employer stating the arrangement, so it counts on its own.
    //
    // A description is prose. Ads routinely mention remote work in order to *deny* it ("we do not
    // offer remote-only roles") or to describe unrelated things ("remote hands", "isRemote:false"
    // in embedded JSON), so a mention there only counts when it carries an unambiguous phrase —
    // and never when that phrase is negated. RemoteMode.Remote exempts a listing from the R-105
    // radius filter, so a false positive hides an office job on another continent; Unknown is the
    // safe fallback (still radius-filtered, neutral to the ranker).
    internal static RemoteMode InferRemoteMode(string? title, string? location, string? description)
    {
        var strong = Normalize($"{title} | {location}");
        var weak = Normalize(description);

        if (HasEvidence(strong, StrongHybridCue)) return RemoteMode.Hybrid;
        if (HasEvidence(strong, StrongRemoteCue)) return RemoteMode.Remote;
        if (HasEvidence(strong, OnsiteCue)) return RemoteMode.Onsite;
        if (HasEvidence(weak, DescriptionHybridPhrase)) return RemoteMode.Hybrid;
        if (HasEvidence(weak, DescriptionRemotePhrase)) return RemoteMode.Remote;
        if (HasEvidence(weak, OnsiteCue)) return RemoteMode.Onsite;
        return RemoteMode.Unknown;
    }

    // Not every adapter hands us stripped text, and a tag mid-sentence must not hide the negation
    // in "we <strong>do not</strong> offer remote-only roles".
    private static readonly Regex Markup = new(@"<[^>]*>|&[a-z]+;|&#\d+;", PatternOptions);

    private static string Normalize(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : Markup.Replace(text.ToLowerInvariant().Replace('’', '\''), " ");

    private const RegexOptions PatternOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // "work from home" / "hjemmearbejde" describe days spent at home, not a fully remote contract,
    // so they are hybrid evidence — the same reading the Danish wording has always had here.
    private static readonly Regex StrongHybridCue = new(
        @"(?<!\w)(hybrid|hjemmearbejde|hjemmearbejdsdage|hjemmedage|hjemmefra|wfh|work from home)(?!\w)", PatternOptions);

    private static readonly Regex StrongRemoteCue = new(
        @"(?<!\w)(remote|remotely|fjernarbejde|distancearbejde|telecommut\w*)(?!\w)", PatternOptions);

    // "in office" without the hyphen is not a cue: it is almost always "a background in office
    // management" or "proficiency in Office tools".
    private static readonly Regex OnsiteCue = new(
        @"(?<!\w)(on-?site|in-office)(?!\w)", PatternOptions);

    // Phrases that only ever describe a hybrid arrangement. "hybrid" must be followed by the thing
    // it qualifies, which keeps Word's "hybridmultilevel" list markup and prose like "a hybrid of
    // two roles" out. Danish ads say "mulighed for hjemmearbejde" — a possibility of working from
    // home, i.e. hybrid, not full remote.
    private static readonly Regex DescriptionHybridPhrase = new(
        @"(?<!\w)(#li-hybrid"
        + @"|hybrid[-\s/]?(work\w*|model\w*|polic\w+|arbejdsmodel|arbejde|setup|set-up|opsætning|schedule|regime|arrangement\w*|approach|role|roles|position|positions|day|days|office|offices|team|teams)"
        + @"|(work|works|working|arbejder|arbejde)\s+hybrid"
        + @"|hjemmearbejde|hjemmearbejdsdage|hjemmedage|hjemmefra|work from home|wfh"
        + @"|remote\s+work:\s*(possible|muligt)"
        + @"|\d+\s+(days?|dage)\s+(a|per|om)\s+(week|ugen)\s+(in|from|på|fra)\s+(the\s+)?(office|kontoret))(?!\w)",
        PatternOptions);

    // Phrases that only ever describe a fully remote arrangement. A bare "remote" is deliberately
    // absent — that mention is what produced the false positives. So is "remote job(s)", which is
    // job-board navigation chrome, and schema.org's "TELECOMMUTE", which portals emit for office
    // roles.
    private static readonly Regex DescriptionRemotePhrase = new(
        @"(?<!\w)(fully[-\s]remote|100\s?%\s?remote|remote[-\s]only|remote[-\s]first|fully[-\s]distributed"
        + @"|work from anywhere|fjernarbejde|distancearbejde"
        + @"|(work|job)\s+location:?\s+(is\s+)?remote"
        + @"|remote\s+(role|roles|position|positions|contract|opportunity)"
        + @"|(this|it)\s+is\s+a\s+remote)(?!\w)",
        PatternOptions);

    private static bool HasEvidence(string lower, Regex cue)
    {
        foreach (RegexMatch m in cue.Matches(lower))
        {
            if (!PrecededByNegation(lower, m.Index) && !FollowedByNegation(lower, m.Index + m.Length)) return true;
        }
        return false;
    }

    // Negators that attach to a verb and can sit a few words away from the phrase
    // ("we do not currently offer fully remote work").
    private static readonly HashSet<string> ClauseNegators = new(StringComparer.Ordinal)
    {
        "not", "never", "cannot", "can't", "don't", "doesn't", "didn't", "won't", "wouldn't",
        "isn't", "aren't", "unable", "ikke", "hverken",
    };

    // Determiner-style negators that only negate what they sit directly in front of
    // ("no remote option") — further away they usually belong to another phrase.
    private static readonly HashSet<string> AdjacentNegators = new(StringComparer.Ordinal)
    {
        "no", "without", "rarely", "ingen", "uden", "sjældent",
    };

    private const int ClauseNegatorTokens = 8;
    private const int AdjacentNegatorTokens = 3;

    private static readonly Regex WordToken = new(@"[\p{L}\p{N}]+(?:'[\p{L}]+)?", PatternOptions);

    // A dash between words opens a new statement ("don't miss out - this is a 100% remote position"),
    // so the negator on its left governs nothing on its right. A hyphen *inside* a word is not a
    // break: "we do not offer full-time remote roles" must keep reaching its "not".
    private static bool IsClauseBreak(string lower, int i)
    {
        var c = lower[i];
        if (c is '.' or ';' or ':' or ',' or '!' or '?' or '\n' or '\r' or '(' or ')' or '|' or '•' or '–' or '—') return true;
        return c is '-' && !(i > 0 && char.IsLetterOrDigit(lower[i - 1])
            && i + 1 < lower.Length && char.IsLetterOrDigit(lower[i + 1]));
    }

    private static bool PrecededByNegation(string lower, int start)
    {
        var clauseStart = start;
        while (clauseStart > 0 && !IsClauseBreak(lower, clauseStart - 1)) clauseStart--;

        var tokens = WordToken.Matches(lower[clauseStart..start]);
        var distance = 1;
        for (var i = tokens.Count - 1; i >= 0 && distance <= ClauseNegatorTokens; i--, distance++)
        {
            var token = tokens[i].Value;
            if (ClauseNegators.Contains(token)) return true;
            if (distance <= AdjacentNegatorTokens && AdjacentNegators.Contains(token)) return true;
        }
        return false;
    }

    // What a trailing "no"/"ingen" must be talking about for the denial to be about this phrase.
    // "100% remote, no relocation needed" and "fjernarbejde, ingen fast arbejdsplads" are offers.
    private const string RemoteNoun =
        @"(remote|remotely|fjernarbejde|distancearbejde|hjemmearbejde|hjemmefra|telecommut\w*"
        + @"|home[-\s]office|work\s+from\s+home|option|options|mulighed|muligheder)";

    // "fully remote is not offered", "hjemmearbejde: ikke oplyst" — the denial trails the phrase.
    // Unlike the backward scan a dash does not stop this one: a trailing dash clause is usually the
    // elaboration that carries the denial ("hjemmearbejde – ikke muligt").
    private static readonly Regex TrailingNegation = new(
        @"^[\s:,\-–—]*((is|are|was|were|er|bliver|vil|kan|tilbydes|udbydes)\s+)?(not|ikke)(?!\w)"
        + @"|^[\s:,\-–—]*(no|ingen)\s+(\w+\s+){0,2}" + RemoteNoun + @"(?!\w)"
        + @"|(?<!\w)(not|ikke)\s+(offered|available|possible|permitted|allowed|disclosed|an\s+option|oplyst|muligt|mulig|mulighed)(?!\w)",
        PatternOptions);

    private const int TrailingNegationWindow = 48;

    private static bool FollowedByNegation(string lower, int end)
    {
        var limit = Math.Min(lower.Length, end + TrailingNegationWindow);
        var stop = end;
        while (stop < limit && lower[stop] is not ('.' or ';' or '!' or '?' or '\n' or '\r')) stop++;
        return stop > end && TrailingNegation.IsMatch(lower[end..stop]);
    }
}
