using Jobmatch.Domain;
using Jobmatch.Domain.Runs;
using Jobmatch.Search.Locations;

namespace Jobmatch.Search.Deduplication;

/// <summary>What the two dedupe passes together left standing, and what they merged getting there.</summary>
public sealed record DedupeOutcome(
    IReadOnlyList<Listing> Deduped,
    IReadOnlyList<DedupeGroup> Merges,
    ProbabilisticDedupeResult Probabilistic);

/// <summary>
/// The deduplication phase. Two passes, always in this order (R-115, R-117): the exact-key merge,
/// then the probabilistic same-ad merge over its survivors — so no duplicate ad reaches the scored
/// list, the LLM judge budget, or the shortlist.
/// </summary>
public static class DuplicateMerger
{
    public static DedupeOutcome Merge(IReadOnlyList<Listing> listings, Gazetteer places)
    {
        var exact = Deduper.Deduplicate(listings, places);
        var probabilistic = ProbabilisticDeduper.Merge(exact.Deduped, new ProbabilisticMatcher(places));

        return new DedupeOutcome(
            Deduped: probabilistic.Deduped,
            Merges: [.. exact.Merges, .. probabilistic.Merges],
            Probabilistic: probabilistic);
    }
}
