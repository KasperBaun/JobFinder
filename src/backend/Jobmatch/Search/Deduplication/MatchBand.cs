namespace Jobmatch.Search.Deduplication;

/// <summary>Verdict band of a probabilistic same-ad comparison (R-116).</summary>
public enum MatchBand
{
    Distinct,
    Possible,
    SameAd,
}
