using Jobmatch.Deduplication;
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
    /// The first four are score-independent (<see cref="ClassifyScoreIndependentDrop"/>) and
    /// are therefore also what decides which listings are worth an LLM verdict; the last two
    /// read the post-judge score and can only be settled afterwards.
    /// The legacy Ranker.Rank/Filter path (tests only) deliberately has no radius filter.
    /// </summary>
    private sealed record DropClassification(string Reason, string Context, IReadOnlyDictionary<string, object> Args);

    private static DropClassification? ClassifyDrop(Match m, RankingConfig ranking, double minScore, RadiusFilter? radius)
    {
        if (ClassifyScoreIndependentDrop(m, ranking, radius) is DropClassification drop) return drop;

        if (m.Score < minScore)
        {
            return new("below_min_score", $"score {m.Score:0.00} below threshold {minScore:0.00}",
                new Dictionary<string, object> { ["score"] = m.Score, ["threshold"] = minScore });
        }

        return null;
    }

    private static DropClassification? ClassifyScoreIndependentDrop(Match m, RankingConfig ranking, RadiusFilter? radius)
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

    /// <summary>A scored match absorbed into a shortlist slot as a sighting of the same ad.</summary>
    internal sealed record AbsorbedSighting(Match Match, double Probability);

    /// <summary>The full outcome of shortlist selection: the slots, the classified drops, which
    /// absorbed matches sit behind which slot, and the pairs the matcher could not settle.</summary>
    internal sealed record ShortlistSelection(
        List<Match> Shortlist,
        List<DroppedEntry> Dropped,
        IReadOnlyDictionary<string, IReadOnlyList<AbsorbedSighting>> SightingsByPrimary,
        IReadOnlyList<PossibleDuplicate> PossibleDuplicates);

    /// <summary>Splits scored matches into the top-N shortlist (by score) and the dropped remainder
    /// (classified drops plus everything beyond top-N). With a matcher, a candidate that is the
    /// same ad as an already-seated slot folds into it as a sighting (R-117) instead of taking a
    /// slot — or, beyond the cut, instead of a beyond_top_n entry.</summary>
    internal static ShortlistSelection BuildShortlist(
        IReadOnlyList<Match> scoredAll, RankingConfig ranking, double minScore, int topN,
        RadiusFilter? radius, ProbabilisticMatcher? matcher = null)
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
        var shortlist = new List<Match>();
        var overflow = new List<Match>();
        var sightings = new Dictionary<string, List<AbsorbedSighting>>(StringComparer.Ordinal);
        var possible = new List<PossibleDuplicate>();

        foreach (var m in ordered)
        {
            if (TryAbsorbAsSighting(m, shortlist, matcher, sightings, possible, dropped)) continue;
            if (shortlist.Count < topN) shortlist.Add(m);
            else overflow.Add(m);
        }

        var total = shortlist.Count + overflow.Count;
        for (var i = 0; i < overflow.Count; i++)
        {
            var rank = shortlist.Count + i + 1;
            dropped.Add(BuildDroppedEntry(overflow[i], "beyond_top_n", $"rank {rank} of {total} (top {topN} taken)",
                new Dictionary<string, object> { ["rank"] = rank, ["total"] = total, ["topN"] = topN }));
        }

        return new ShortlistSelection(
            shortlist,
            dropped,
            sightings.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<AbsorbedSighting>)kvp.Value, StringComparer.Ordinal),
            possible);
    }

    // A probability groups but never deletes (R-117): SameAd folds the candidate behind the slot
    // it duplicates — recorded as a drop so the audit trail stays complete — while Possible is
    // only noted for the duplicates view. Absorption also applies beyond the cut, so an ad seen
    // through three portals carries all its sightings.
    private static bool TryAbsorbAsSighting(
        Match candidate,
        List<Match> shortlist,
        ProbabilisticMatcher? matcher,
        Dictionary<string, List<AbsorbedSighting>> sightings,
        List<PossibleDuplicate> possible,
        List<DroppedEntry> dropped)
    {
        if (matcher is null) return false;
        foreach (var slot in shortlist)
        {
            var verdict = matcher.Compare(slot.Listing, candidate.Listing);
            if (verdict.Band == MatchBand.SameAd)
            {
                // One ad appears once per portal: when a slot has already absorbed a sighting
                // from this portal, a second same-portal claimant is that portal's *other* req —
                // a null-location listing wildcards every city — so it keeps its own candidacy
                // and the pair is only recorded.
                if (sightings.TryGetValue(slot.Listing.Id, out var list)
                    && list.Any(s => s.Match.Listing.Portal == candidate.Listing.Portal))
                {
                    possible.Add(new PossibleDuplicate(slot.Listing.Id, candidate.Listing.Id, Math.Round(verdict.Probability, 2)));
                    continue;
                }
                if (list is null)
                    sightings[slot.Listing.Id] = list = [];
                list.Add(new AbsorbedSighting(candidate, verdict.Probability));
                dropped.Add(BuildDroppedEntry(candidate, "duplicate_of_shortlisted",
                    FormattableString.Invariant(
                        $"same ad as shortlisted '{slot.Listing.Title}' (probability {verdict.Probability:0.00})"),
                    new Dictionary<string, object>
                    {
                        ["ofId"] = slot.Listing.Id,
                        ["ofTitle"] = slot.Listing.Title,
                        ["probability"] = Math.Round(verdict.Probability, 2),
                    }));
                return true;
            }
            if (verdict.Band == MatchBand.Possible)
                possible.Add(new PossibleDuplicate(slot.Listing.Id, candidate.Listing.Id, Math.Round(verdict.Probability, 2)));
        }
        return false;
    }
}
