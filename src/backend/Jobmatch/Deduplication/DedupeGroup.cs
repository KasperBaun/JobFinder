namespace Jobmatch.Deduplication;

/// <summary>
/// One canonical listing plus the IDs of duplicates that collapsed into it.
/// MergedFromIds is empty for canonicals that had no duplicates.
/// </summary>
public sealed record DedupeGroup(
    string CanonicalId,
    IReadOnlyList<string> MergedFromIds);
