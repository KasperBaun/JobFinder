using Jobmatch.Domain;
using Jobmatch.Domain.Runs;

namespace Jobmatch.Pipeline.Deduplication;

/// <summary>
/// The probabilistic second dedupe pass (R-117), run on the exact-key <see cref="Deduper"/>'s
/// survivors *before* ranking, so no duplicate ad reaches the scored list, the LLM judge or the
/// shortlist. A listing the matcher deems the same ad as an already-kept canonical is absorbed
/// as a sighting; a Possible verdict never merges — it is recorded for the duplicates audit
/// view when strong enough to be worth a human glance.
/// <para>
/// Listings are processed most-informative first (location present, fuller description), so the
/// copy that survives is the one the ranker can do the most with — the located Workday req
/// outlives its null-location jobindex re-listing, not the other way round. Two safety rules
/// bound the merge: the matcher never yields SameAd within one portal, and a canonical absorbs
/// at most one listing per portal — an ad appears once per portal, so a second same-portal
/// claimant is that portal's other req and survives with the pair recorded.
/// </para>
/// </summary>
public static class ProbabilisticDeduper
{
    // Below this the Possible band is dominated by same-title/other-city postings — real
    // distinct roles (p 0.33, or 0.5 once recency agrees) that would drown the audit view.
    // Recorded pairs start where a human would actually hesitate.
    private const double PossibleRecordFloor = 0.6;

    private sealed record Canonical(MatchFeatures Features, List<AbsorbedListing> Absorbed);

    public static ProbabilisticDedupeResult Merge(IReadOnlyList<Listing> listings, ProbabilisticMatcher matcher)
    {
        var byCompany = new Dictionary<string, List<Canonical>>(StringComparer.Ordinal);
        var absorbedIds = new HashSet<string>(StringComparer.Ordinal);
        var sightings = new Dictionary<string, IReadOnlyList<AbsorbedListing>>(StringComparer.Ordinal);
        var possible = new List<PossibleDuplicate>();

        foreach (var listing in listings
            .OrderByDescending(Informativeness)
            .ThenBy(l => l.Id, StringComparer.Ordinal))
        {
            var features = matcher.Extract(listing);
            if (features.CompanyKey.Length == 0)
            {
                continue; // No blocking key — never compared, always kept.
            }

            // Blocked on the first company token, not the whole name, so "Danske Bank" and
            // "Danske Bank Group" meet — the matcher's company gate decides whether they match.
            var space = features.CompanyKey.IndexOf(' ');
            var blockKey = space < 0 ? features.CompanyKey : features.CompanyKey[..space];
            if (!byCompany.TryGetValue(blockKey, out var block))
            {
                byCompany[blockKey] = [new Canonical(features, [])];
                continue;
            }

            if (Place(block, features, matcher, possible) is { } target)
            {
                target.Canonical.Absorbed.Add(new AbsorbedListing(listing, target.Probability));
                absorbedIds.Add(listing.Id);
            }
            else
            {
                block.Add(new Canonical(features, []));
            }
        }

        var merges = new List<DedupeGroup>();
        foreach (var canonical in byCompany.Values.SelectMany(b => b).Where(c => c.Absorbed.Count > 0))
        {
            merges.Add(new DedupeGroup(canonical.Features.Listing.Id, [.. canonical.Absorbed.Select(a => a.Listing.Id)]));
            sightings[canonical.Features.Listing.Id] = canonical.Absorbed;
        }

        var deduped = listings.Where(l => !absorbedIds.Contains(l.Id)).ToList();
        // Cross-portal pairs first (a possible matcher miss), then strongest hesitation —
        // the audit view reads top-down, and same-portal employer re-posts are its long tail.
        possible.Sort((x, y) => x.SamePortal != y.SamePortal
            ? x.SamePortal.CompareTo(y.SamePortal)
            : y.Probability.CompareTo(x.Probability));
        return new ProbabilisticDedupeResult(deduped, merges, sightings, possible);
    }

    /// <summary>The canonical this listing folds into, or null to keep it. SameAd wins by
    /// probability; a canonical whose group already holds this listing's portal cannot win —
    /// the pair is recorded instead, like every strong-enough Possible verdict.</summary>
    private static (Canonical Canonical, double Probability)? Place(
        List<Canonical> block, MatchFeatures features, ProbabilisticMatcher matcher, List<PossibleDuplicate> possible)
    {
        (Canonical Canonical, double Probability)? best = null;
        foreach (var canonical in block)
        {
            var verdict = matcher.Compare(canonical.Features, features);
            var portalTaken = canonical.Absorbed.Any(a => a.Listing.Portal == features.Listing.Portal);
            if (verdict.Band == MatchBand.SameAd && !portalTaken)
            {
                if (best is null || verdict.Probability > best.Value.Probability)
                    best = (canonical, Math.Round(verdict.Probability, 2));
                continue;
            }
            var recordable = verdict.Band == MatchBand.Possible || (verdict.Band == MatchBand.SameAd && portalTaken);
            if (recordable && verdict.Probability >= PossibleRecordFloor)
                possible.Add(new PossibleDuplicate(
                    canonical.Features.Listing.Id,
                    features.Listing.Id,
                    Math.Round(verdict.Probability, 2),
                    SamePortal: canonical.Features.Listing.Portal == features.Listing.Portal));
        }
        return best;
    }

    // Ranking has not happened yet, so the survivor is chosen by what the ranker (and the
    // user's card) can do with it: a located listing beats a wildcard, fuller text beats a
    // stub, a dated one beats undated.
    private static int Informativeness(Listing l) =>
        (string.IsNullOrWhiteSpace(l.Location) ? 0 : 4)
        + (l.Description.Length > 200 ? 2 : 0)
        + (l.PostedAt is null ? 0 : 1);
}
