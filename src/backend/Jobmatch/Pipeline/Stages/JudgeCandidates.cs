using Jobmatch.Pipeline.Geo;
using Jobmatch.Pipeline.Ranking;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Pipeline.Stages;

/// <summary>Which listings are worth spending a verdict on, for a given pass.</summary>
public static class JudgeCandidates
{
    /// <summary>
    /// The judge budget (llm.top_n) buys verdicts only for listings that can still reach the
    /// shortlist. Spending it on the raw keyword top-N wastes calls on listings the hard
    /// filters discard moments later, which leaves genuine shortlist entries unjudged and
    /// silently keyword-scored. Score-dependent drops are excluded — they are settled from the
    /// blended score, so they cannot be known yet.
    /// </summary>
    public static List<Match> ForFirstPass(
        IReadOnlyList<Match> scored,
        RankingConfig ranking,
        RadiusFilter? radius,
        int topN)
    {
        var eligible = scored
            .Where(m => ShortlistBuilder.ClassifyScoreIndependentDrop(m, ranking, radius) is null)
            .OrderByDescending(m => m.Score);
        return topN <= 0 ? eligible.ToList() : eligible.Take(topN).ToList();
    }

    /// <summary>The shortlist as the blended scores now stand, minus everything already offered to
    /// the judge — i.e. exactly the entries that would render as "not reviewed by AI".</summary>
    public static List<Match> UnjudgedShortlist(
        IReadOnlyList<Match> scored,
        RankingConfig ranking,
        double minScore,
        int topN,
        RadiusFilter? radius,
        IReadOnlySet<string> attempted)
    {
        var (shortlist, _) = ShortlistBuilder.BuildShortlist(scored, ranking, minScore, topN, radius);
        return [.. shortlist.Where(m => !attempted.Contains(m.Listing.Id))];
    }
}
