namespace Jobmatch.Deduplication;

/// <summary>
/// Outcome of a probabilistic pair comparison: per-field log-odds evidence, the blended
/// probability, and the band it falls in. The field evidence is kept so a verdict stays
/// explainable — in tests, tuning and audit views — rather than being a bare number.
/// </summary>
public sealed record MatchVerdict(
    MatchBand Band,
    double Probability,
    double TitleEvidence,
    double LocationEvidence,
    double RecencyEvidence,
    double DescriptionEvidence = 0);
