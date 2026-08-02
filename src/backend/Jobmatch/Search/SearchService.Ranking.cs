using Jobmatch.Geo;
using Jobmatch.Models;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    /// <summary>
    /// Classifies why a scored match would be excluded from the shortlist. Order of
    /// precedence: above_max_age (hard temporal cutoff) → outside_radius (hard spatial
    /// cutoff — remote-exempt; unresolvable locations pass) → missing_required_primary →
    /// disqualifier → below_min_score. Returns null if the match should pass to
    /// shortlist consideration. beyond_top_n is decided after sorting, not here.
    /// The legacy Ranker.Rank/Filter path (tests only) deliberately has no radius filter.
    /// </summary>
    private sealed record DropClassification(string Reason, string Context, IReadOnlyDictionary<string, object> Args);

    private static DropClassification? ClassifyDrop(Match m, RankingConfig ranking, double minScore, RadiusFilter? radius)
    {
        if (ranking.MaxAgeDays is int maxAge && m.Listing.PostedAt is DateTimeOffset posted)
        {
            var ageDays = (DateTimeOffset.UtcNow - posted).TotalDays;
            if (ageDays > maxAge)
            {
                var days = (int)Math.Round(ageDays);
                return new("above_max_age", $"posted {days} days ago, max {maxAge}",
                    new Dictionary<string, object> { ["days"] = days, ["maxAge"] = maxAge });
            }
        }

        if (radius?.Evaluate(m.Listing) is RadiusVerdict verdict)
        {
            return new("outside_radius",
                $"located ~{verdict.Km} km away ({verdict.Place}), max {verdict.MaxKm} km",
                new Dictionary<string, object>
                {
                    ["km"] = verdict.Km,
                    ["maxKm"] = verdict.MaxKm,
                    ["place"] = verdict.Place,
                });
        }

        if (ranking.RequirePrimaryStackHit && m.Reasoning.PrimaryStackHits.Count == 0)
        {
            return new("missing_required_primary", "no primary-stack keyword matched in title or description",
                new Dictionary<string, object>());
        }

        if (m.Reasoning.DisqualifierHits.Count > 0)
        {
            return new("disqualifier", $"matched disqualifier: {string.Join(", ", m.Reasoning.DisqualifierHits)}",
                new Dictionary<string, object> { ["hits"] = m.Reasoning.DisqualifierHits });
        }

        if (m.Score < minScore)
        {
            return new("below_min_score", $"score {m.Score:0.00} below threshold {minScore:0.00}",
                new Dictionary<string, object> { ["score"] = m.Score, ["threshold"] = minScore });
        }

        return null;
    }

    private static DroppedEntry BuildDroppedEntry(Match m, string reason, string? context, IReadOnlyDictionary<string, object>? args = null) => new(
        Id: m.Listing.Id,
        Title: m.Listing.Title,
        Company: m.Listing.Company,
        Score: m.Score,
        Reason: reason,
        Context: context,
        ContextArgs: args);

    /// <summary>Splits scored matches into the top-N shortlist (by score) and the dropped remainder
    /// (classified drops plus everything beyond top-N).</summary>
    private static (List<Match> Shortlist, List<DroppedEntry> Dropped) BuildShortlist(
        IReadOnlyList<Match> scoredAll, RankingConfig ranking, double minScore, int topN, RadiusFilter? radius)
    {
        var dropped = new List<DroppedEntry>();
        var passed = new List<Match>();
        foreach (var m in scoredAll)
        {
            var reason = ClassifyDrop(m, ranking, minScore, radius);
            if (reason is null)
                passed.Add(m);
            else
                dropped.Add(BuildDroppedEntry(m, reason.Reason, reason.Context, reason.Args));
        }

        var ordered = passed.OrderByDescending(m => m.Score).ToList();
        var shortlist = ordered.Take(topN).ToList();
        for (var i = topN; i < ordered.Count; i++)
        {
            var m = ordered[i];
            dropped.Add(BuildDroppedEntry(m, "beyond_top_n", $"rank {i + 1} of {ordered.Count} (top {topN} taken)",
                new Dictionary<string, object> { ["rank"] = i + 1, ["total"] = ordered.Count, ["topN"] = topN }));
        }

        return (shortlist, dropped);
    }
}
