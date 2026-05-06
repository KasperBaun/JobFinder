using Jobmatch.Models;

namespace Jobmatch.Deduplication;

/// <summary>
/// Output of <see cref="Deduper.Deduplicate"/>. Deduped is the canonical
/// list; Merges describes which originals collapsed into which canonical
/// (only canonicals that absorbed at least one duplicate appear).
/// </summary>
public sealed record DedupeResult(
    IReadOnlyList<Listing> Deduped,
    IReadOnlyList<DedupeGroup> Merges);
