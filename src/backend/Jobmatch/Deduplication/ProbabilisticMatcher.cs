using Jobmatch.Geo;
using Jobmatch.Models;

namespace Jobmatch.Deduplication;

/// <summary>
/// Fellegi–Sunter-style scorer deciding whether two listings are the same ad seen through two
/// portals. Each field contributes hand-set log₂-odds evidence; the sum becomes a probability
/// and a <see cref="MatchBand"/>. Canonical company equality is the blocking key — pairs with
/// different or missing companies are Distinct outright, so callers may compare freely without
/// candidate pre-selection.
/// <para>
/// This matcher is deliberately separate from the exact-key <see cref="Deduper"/> (R-115): the
/// deduper's merge is destructive and must never gamble, while these verdicts are used
/// non-destructively at shortlist time (R-116) — SameAd folds into an existing slot as a
/// sighting, Possible is recorded for audit, and no listing is ever deleted on a probability.
/// </para>
/// </summary>
public sealed class ProbabilisticMatcher(Gazetteer? gazetteer = null)
{
    // Log₂-odds weights, hand-tuned against run 20260806-113247-dd3dc6 (docs/tasks/T-013).
    // The prior encodes that two same-company listings that both survived the filters are
    // usually different roles; title evidence must overcome it, and a seniority conflict or a
    // resolved-location disagreement is designed to sink any title similarity short of certainty.
    private const double Prior = -4;
    private const double TitleExact = 10;
    private const double TitleNearIdentical = 8;
    private const double TitleSimilar = 3;
    private const double TitleWeak = -2;
    private const double TitleDifferent = -8;
    private const double NearIdenticalJaccard = 0.85;
    private const double SimilarJaccard = 0.65;
    private const double WeakJaccard = 0.45;
    private const double SeniorityConflictPenalty = -9;
    private const double StackConflictPenalty = -9;
    private const double LocationAgrees = 3;
    private const double LocationDiffers = -7;
    private const double RecencyClose = 1;
    private const double RecencyFar = -2;
    private const double CompatibleKm = 30;
    private const double CloseDays = 14;
    private const double FarDays = 60;
    private const double SameAdProbability = 0.90;
    private const double PossibleProbability = 0.30;

    /// <summary>Precomputes a listing's comparison features once, so a caller comparing one
    /// listing against many (the dedupe pass) does not re-normalise per pair.</summary>
    public MatchFeatures Extract(Listing listing)
    {
        var title = Deduper.Normalise(listing.Title);
        return new MatchFeatures(
            listing,
            Deduper.NormaliseCompany(listing.Company),
            title,
            TitleSimilarity.Tokenise(title),
            Deduper.NormaliseLocation(listing.Location, gazetteer),
            gazetteer?.ResolveSites(listing.Location, null) ?? []);
    }

    public MatchVerdict Compare(Listing a, Listing b) => Compare(Extract(a), Extract(b));

    public MatchVerdict Compare(MatchFeatures a, MatchFeatures b)
    {
        if (a.CompanyKey.Length == 0 || a.CompanyKey != b.CompanyKey)
            return new MatchVerdict(MatchBand.Distinct, 0, 0, 0, 0);

        var title = TitleEvidence(a, b);
        var location = LocationEvidence(a, b);
        var recency = RecencyEvidence(a.Listing.PostedAt, b.Listing.PostedAt);
        var probability = ToProbability(Prior + title + location + recency);
        var band = BandOf(probability);

        // Within one portal the exact-key deduper (R-115) has already merged true duplicates by
        // URL; two distinct URLs on the same source are almost always two ads — "Senior X" and
        // "X" as separate reqs — so a same-portal pair can reach Possible but never SameAd.
        if (band == MatchBand.SameAd && a.Listing.Portal == b.Listing.Portal) band = MatchBand.Possible;
        return new MatchVerdict(band, probability, title, location, recency);
    }

    private static double TitleEvidence(MatchFeatures a, MatchFeatures b)
    {
        if (a.TitleKey == b.TitleKey) return TitleExact;

        var evidence = TitleSimilarity.Jaccard(a.TitleTokens, b.TitleTokens) switch
        {
            >= NearIdenticalJaccard => TitleNearIdentical,
            >= SimilarJaccard => TitleSimilar,
            >= WeakJaccard => TitleWeak,
            _ => TitleDifferent,
        };
        if (TitleSimilarity.SeniorityConflicts(a.TitleTokens, b.TitleTokens)) evidence += SeniorityConflictPenalty;
        if (TitleSimilarity.StackConflicts(a.TitleTokens, b.TitleTokens)) evidence += StackConflictPenalty;
        return evidence;
    }

    private static double LocationEvidence(MatchFeatures a, MatchFeatures b)
    {
        // A missing location is a wildcard, not a mismatch — jobindex re-listings often omit it.
        if (a.LocationKey.Length == 0 || b.LocationKey.Length == 0) return 0;
        if (a.LocationKey == b.LocationKey) return LocationAgrees;

        // Differing keys are a *conflict* (Manila vs København) only when the places genuinely
        // disagree. A granularity difference — "Denmark" vs "København V", a multi-country site
        // list vs one of its cities, "Nordhavn, København Ø" vs "København Ø" — is neutral:
        // any resolved site within CompatibleKm of the other side's, or a country-only claim
        // covering the other side's country, is compatible with being the same ad.
        return SitesCompatible(a.Sites, b.Sites) ? 0 : LocationDiffers;
    }

    private static bool SitesCompatible(IReadOnlyList<GeoPlace> a, IReadOnlyList<GeoPlace> b)
    {
        if (a.Count == 0 || b.Count == 0) return false;
        foreach (var siteA in a)
        {
            foreach (var siteB in b)
            {
                if (siteA.Type == GeoPlaceType.Country || siteB.Type == GeoPlaceType.Country)
                {
                    if (string.Equals(siteA.CountryCode, siteB.CountryCode, StringComparison.OrdinalIgnoreCase))
                        return true;
                    continue;
                }
                if (GeoDistance.HaversineKm(siteA.Latitude, siteA.Longitude, siteB.Latitude, siteB.Longitude) <= CompatibleKm)
                    return true;
            }
        }
        return false;
    }

    private static double RecencyEvidence(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null || b is null) return 0;
        var days = Math.Abs((a.Value - b.Value).TotalDays);
        return days <= CloseDays ? RecencyClose : days > FarDays ? RecencyFar : 0;
    }

    private static double ToProbability(double logOdds) => 1 / (1 + Math.Pow(2, -logOdds));

    private static MatchBand BandOf(double probability) => probability switch
    {
        >= SameAdProbability => MatchBand.SameAd,
        >= PossibleProbability => MatchBand.Possible,
        _ => MatchBand.Distinct,
    };
}
