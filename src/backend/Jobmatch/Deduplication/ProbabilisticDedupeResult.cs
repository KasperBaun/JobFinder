using Jobmatch.Models;

namespace Jobmatch.Deduplication;

/// <summary>A listing absorbed into a canonical as a sighting of the same ad (R-117).</summary>
public sealed record AbsorbedListing(Listing Listing, double Probability);

/// <summary>
/// Outcome of the probabilistic dedupe pass: the surviving listings, the merge groups it adds
/// on top of the exact-key ones, the absorbed listings behind each canonical (for "also seen
/// on" sightings), and the pairs it left unsettled for the duplicates audit view.
/// </summary>
public sealed record ProbabilisticDedupeResult(
    IReadOnlyList<Listing> Deduped,
    IReadOnlyList<DedupeGroup> Merges,
    IReadOnlyDictionary<string, IReadOnlyList<AbsorbedListing>> SightingsByCanonical,
    IReadOnlyList<PossibleDuplicate> PossibleDuplicates);
