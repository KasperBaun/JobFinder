namespace Jobmatch.Models;

public sealed record RankingConfig(
    RankingWeights Weights,
    double DisqualifierPenalty,
    int TopN,
    double FreshnessHalfLifeDays,
    double MinScoreToInclude);

public sealed record RankingWeights(
    double PrimaryStack,
    double SecondaryStack,
    double Seniority,
    double LocationRemote,
    double Domain,
    double Freshness)
{
    public double Sum() => PrimaryStack + SecondaryStack + Seniority + LocationRemote + Domain + Freshness;
}
