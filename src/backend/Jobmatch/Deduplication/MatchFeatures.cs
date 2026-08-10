using Jobmatch.Geo;
using Jobmatch.Models;

namespace Jobmatch.Deduplication;

/// <summary>
/// A listing's precomputed comparison features — normalised once via
/// <see cref="ProbabilisticMatcher.Extract"/> so comparing one listing against many stays cheap.
/// Sites are the gazetteer-resolved places of the raw location string, used to tell a location
/// *conflict* (Manila vs København) from a granularity difference ("Denmark" vs "København V").
/// </summary>
public sealed record MatchFeatures(
    Listing Listing,
    string CompanyKey,
    string TitleKey,
    IReadOnlySet<string> TitleTokens,
    string LocationKey,
    IReadOnlyList<GeoPlace> Sites);
