namespace Jobmatch.Search;

/// <summary>
/// All raw listings returned by a single provider in a single run, before dedupe.
/// </summary>
public sealed record ProviderRaw(
    string Provider,
    IReadOnlyList<RawListing> Listings);
