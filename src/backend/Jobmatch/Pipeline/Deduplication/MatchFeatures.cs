using Jobmatch.Domain;
using Jobmatch.Pipeline.Geo;

namespace Jobmatch.Pipeline.Deduplication;

/// <summary>
/// A listing's precomputed comparison features — normalised once via
/// <see cref="ProbabilisticMatcher.Extract"/> so comparing one listing against many stays cheap.
/// Sites are the gazetteer-resolved places of the raw location string, used to tell a location
/// *conflict* (Manila vs København) from a granularity difference ("Denmark" vs "København V").
/// Description shingles are lazy: body text only enters the comparison for pairs the other
/// fields already put near a decision boundary.
/// </summary>
public sealed record MatchFeatures(
    Listing Listing,
    string CompanyKey,
    IReadOnlySet<string> CompanyTokens,
    string TitleKey,
    IReadOnlySet<string> TitleTokens,
    string LocationKey,
    IReadOnlyList<GeoPlace> Sites,
    Lazy<IReadOnlySet<int>> DescriptionShingles);
