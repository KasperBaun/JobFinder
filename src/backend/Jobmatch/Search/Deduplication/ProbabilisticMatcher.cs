using Jobmatch.Domain;
using Jobmatch.Search.Locations;

namespace Jobmatch.Search.Deduplication;

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
    private const double CompanyLoose = -1;
    private const double DescriptionNearCopy = 6;
    private const double DescriptionOverlap = 2;
    private const double DescriptionDisjoint = -3;
    private const double NearCopyContainment = 0.6;
    private const double OverlapContainment = 0.3;
    private const double DisjointContainment = 0.05;
    private const int MinShingles = 30;
    private const int SubstantialShingles = 150;
    private const double DescriptionGateFloor = -3;
    private const double CloseDays = 14;
    private const double FarDays = 60;
    private const double SameAdProbability = 0.90;
    private const double PossibleProbability = 0.30;

    /// <summary>Precomputes a listing's comparison features once, so a caller comparing one
    /// listing against many (the dedupe pass) does not re-normalise per pair.</summary>
    public MatchFeatures Extract(Listing listing)
    {
        var title = Deduper.Normalise(listing.Title);
        var company = Deduper.NormaliseCompany(listing.Company);
        return new MatchFeatures(
            listing,
            company,
            company.Length == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : company.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal),
            title,
            TitleSimilarity.Tokenise(title),
            Deduper.NormaliseLocation(listing.Location, gazetteer),
            gazetteer?.ResolveSites(listing.Location, null) ?? [],
            new Lazy<IReadOnlySet<int>>(
                () => DescriptionSimilarity.Shingles(listing.Description), LazyThreadSafetyMode.None));
    }

    public MatchVerdict Compare(Listing a, Listing b) => Compare(Extract(a), Extract(b));

    public MatchVerdict Compare(MatchFeatures a, MatchFeatures b)
    {
        if (CompanyEvidence(a, b) is not double company)
            return new MatchVerdict(MatchBand.Distinct, 0, 0, 0, 0);

        var title = TitleEvidence(a, b);
        var (location, resolvedConflict) = LocationEvidence(a, b);
        var recency = RecencyEvidence(a.Listing.PostedAt, b.Listing.PostedAt);
        var description = DescriptionEvidence(a, b, resolvedConflict, Prior + company + title + location + recency);
        var probability = ToProbability(Prior + company + title + location + recency + description);
        var band = BandOf(probability);

        // Within one portal the exact-key deduper (R-115) has already merged true duplicates by
        // URL; two distinct URLs on the same source are almost always two ads — "Senior X" and
        // "X" as separate reqs — so a same-portal pair can reach Possible but never SameAd.
        if (band == MatchBand.SameAd && a.Listing.Portal == b.Listing.Portal) band = MatchBand.Possible;
        return new MatchVerdict(band, probability, title, location, recency, description);
    }

    // Identical canonical companies compare at full strength; a token-subset ("Danske Bank" vs
    // "Danske Bank Group", "twoday" vs "twoday Denmark") is the same employer under a longer
    // convention and costs a little; anything else — including missing — never compares, since
    // "Danske Bank" vs "Danske Spil" sharing a token is exactly the trap subset matching avoids.
    private static double? CompanyEvidence(MatchFeatures a, MatchFeatures b)
    {
        if (a.CompanyKey.Length == 0 || b.CompanyKey.Length == 0) return null;
        if (a.CompanyKey == b.CompanyKey) return 0;
        if (a.CompanyTokens.IsSubsetOf(b.CompanyTokens) || b.CompanyTokens.IsSubsetOf(a.CompanyTokens))
            return CompanyLoose;
        return null;
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

    // The bool distinguishes a *resolved* conflict (both sides name real, incompatible places —
    // Manila vs København) from an unresolved one ("Headquarters (IT)"): only the former is
    // allowed to suppress description evidence, because two reqs of one employer share template
    // text and near-copy bodies must not overrule places we know disagree.
    private static (double Evidence, bool ResolvedConflict) LocationEvidence(MatchFeatures a, MatchFeatures b)
    {
        // A missing location is a wildcard, not a mismatch — jobindex re-listings often omit it.
        if (a.LocationKey.Length == 0 || b.LocationKey.Length == 0) return (0, false);
        if (a.LocationKey == b.LocationKey) return (LocationAgrees, false);

        // Differing keys are a *conflict* (Manila vs København) only when the places genuinely
        // disagree. A granularity difference — "Denmark" vs "København V", a multi-country site
        // list vs one of its cities, "Nordhavn, København Ø" vs "København Ø" — is neutral:
        // any resolved site within CompatibleKm of the other side's, or a country-only claim
        // covering the other side's country, is compatible with being the same ad.
        if (SitesCompatible(a.Sites, b.Sites)) return (0, false);
        return (LocationDiffers, a.Sites.Count > 0 && b.Sites.Count > 0);
    }

    private double DescriptionEvidence(MatchFeatures a, MatchFeatures b, bool resolvedConflict, double scoreSoFar)
    {
        if (resolvedConflict) return 0;
        // Body text can swing the verdict by [DescriptionDisjoint, DescriptionNearCopy]; pairs
        // the other fields already settled far outside that reach skip the shingle work.
        if (scoreSoFar < DescriptionGateFloor) return 0;

        var shinglesA = a.DescriptionShingles.Value;
        var shinglesB = b.DescriptionShingles.Value;
        if (shinglesA.Count < MinShingles || shinglesB.Count < MinShingles) return 0;

        var containment = DescriptionSimilarity.Containment(shinglesA, shinglesB);
        if (containment >= NearCopyContainment) return DescriptionNearCopy;
        if (containment >= OverlapContainment) return DescriptionOverlap;
        // Only substantial texts that share nothing argue against — a short excerpt or an
        // aggregator's own blurb proves nothing about the ad it fronts.
        if (containment < DisjointContainment
            && shinglesA.Count >= SubstantialShingles && shinglesB.Count >= SubstantialShingles)
        {
            return DescriptionDisjoint;
        }
        return 0;
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
