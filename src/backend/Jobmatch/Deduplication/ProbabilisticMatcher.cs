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
    private const double LocationAgrees = 3;
    private const double LocationDiffers = -7;
    private const double RecencyClose = 1;
    private const double RecencyFar = -2;
    private const double CloseDays = 14;
    private const double FarDays = 60;
    private const double SameAdProbability = 0.90;
    private const double PossibleProbability = 0.30;

    public MatchVerdict Compare(Listing a, Listing b)
    {
        var company = Deduper.NormaliseCompany(a.Company);
        if (company.Length == 0 || company != Deduper.NormaliseCompany(b.Company))
            return new MatchVerdict(MatchBand.Distinct, 0, 0, 0, 0);

        var title = TitleEvidence(a.Title, b.Title);
        var location = LocationEvidence(a.Location, b.Location);
        var recency = RecencyEvidence(a.PostedAt, b.PostedAt);
        var probability = ToProbability(Prior + title + location + recency);
        var band = BandOf(probability);

        // Within one portal the exact-key deduper (R-115) has already merged true duplicates by
        // URL; two distinct URLs on the same source are almost always two ads — "Senior X" and
        // "X" as separate reqs — so a same-portal pair can reach Possible but never SameAd.
        if (band == MatchBand.SameAd && a.Portal == b.Portal) band = MatchBand.Possible;
        return new MatchVerdict(band, probability, title, location, recency);
    }

    private static double TitleEvidence(string a, string b)
    {
        var normalisedA = Deduper.Normalise(a);
        var normalisedB = Deduper.Normalise(b);
        if (normalisedA == normalisedB) return TitleExact;

        var tokensA = TitleSimilarity.Tokenise(normalisedA);
        var tokensB = TitleSimilarity.Tokenise(normalisedB);
        var evidence = TitleSimilarity.Jaccard(tokensA, tokensB) switch
        {
            >= NearIdenticalJaccard => TitleNearIdentical,
            >= SimilarJaccard => TitleSimilar,
            >= WeakJaccard => TitleWeak,
            _ => TitleDifferent,
        };
        if (TitleSimilarity.SeniorityConflicts(tokensA, tokensB)) evidence += SeniorityConflictPenalty;
        return evidence;
    }

    private double LocationEvidence(string? a, string? b)
    {
        var keyA = Deduper.NormaliseLocation(a, gazetteer);
        var keyB = Deduper.NormaliseLocation(b, gazetteer);
        // A missing location is a wildcard, not a mismatch — jobindex re-listings often omit it.
        if (keyA.Length == 0 || keyB.Length == 0) return 0;
        return keyA == keyB ? LocationAgrees : LocationDiffers;
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
