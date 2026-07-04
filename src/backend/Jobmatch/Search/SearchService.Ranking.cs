using Jobmatch.Models;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    /// <summary>
    /// Classifies why a scored match would be excluded from the shortlist. Order of
    /// precedence: above_max_age (hard cutoff) → missing_required_primary →
    /// disqualifier → below_min_score. Returns null if the match should pass to
    /// shortlist consideration. beyond_top_n is decided after sorting, not here.
    /// </summary>
    private static (string Reason, string? Context)? ClassifyDrop(Match m, RankingConfig ranking, double minScore)
    {
        if (ranking.MaxAgeDays is int maxAge && m.Listing.PostedAt is DateTimeOffset posted)
        {
            var ageDays = (DateTimeOffset.UtcNow - posted).TotalDays;
            if (ageDays > maxAge)
            {
                return ("above_max_age", $"posted {(int)Math.Round(ageDays)} days ago, max {maxAge}");
            }
        }

        if (ranking.RequirePrimaryStackHit && m.Reasoning.PrimaryStackHits.Count == 0)
        {
            return ("missing_required_primary", "no primary-stack keyword matched in title or description");
        }

        if (m.Reasoning.DisqualifierHits.Count > 0)
        {
            return ("disqualifier", $"matched disqualifier: {string.Join(", ", m.Reasoning.DisqualifierHits)}");
        }

        if (m.Score < minScore)
        {
            return ("below_min_score", $"score {m.Score:0.00} below threshold {minScore:0.00}");
        }

        return null;
    }

    private static DroppedEntry BuildDroppedEntry(Match m, string reason, string? context) => new(
        Id: m.Listing.Id,
        Title: m.Listing.Title,
        Company: m.Listing.Company,
        Score: m.Score,
        Reason: reason,
        Context: context);

    /// <summary>Splits scored matches into the top-N shortlist (by score) and the dropped remainder
    /// (classified drops plus everything beyond top-N).</summary>
    private static (List<Match> Shortlist, List<DroppedEntry> Dropped) BuildShortlist(
        IReadOnlyList<Match> scoredAll, RankingConfig ranking, double minScore, int topN)
    {
        var dropped = new List<DroppedEntry>();
        var passed = new List<Match>();
        foreach (var m in scoredAll)
        {
            var reason = ClassifyDrop(m, ranking, minScore);
            if (reason is null)
                passed.Add(m);
            else
                dropped.Add(BuildDroppedEntry(m, reason.Value.Reason, reason.Value.Context));
        }

        var ordered = passed.OrderByDescending(m => m.Score).ToList();
        var shortlist = ordered.Take(topN).ToList();
        for (var i = topN; i < ordered.Count; i++)
        {
            var m = ordered[i];
            dropped.Add(BuildDroppedEntry(m, "beyond_top_n", $"rank {i + 1} of {ordered.Count} (top {topN} taken)"));
        }

        return (shortlist, dropped);
    }
}
