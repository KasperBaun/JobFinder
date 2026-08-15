using Jobmatch.Infrastructure.Llm;

namespace Jobmatch.Pipeline.Ranking;

public sealed record RankingConfig(
    RankingWeights Weights,
    double DisqualifierPenalty,
    int TopN,
    double FreshnessHalfLifeDays,
    double MinScoreToInclude,
    int? MaxAgeDays = null,
    bool RequirePrimaryStackHit = false,
    double SeniorityAdjacencyCredit = 1.0,
    double NonEngineeringTitleMultiplier = 0.2,
    double PreferredCompanyBoost = 1.25)
{
    public LocationTierWeights LocationTierWeights { get; init; } = LocationTierWeights.Default;
    public LlmConfig Llm { get; init; } = LlmConfig.Disabled;
}

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

public sealed record LocationTierWeights(
    double City,
    double Metro,
    double Country,
    double Region,
    double Else)
{
    public static LocationTierWeights Default { get; } = new(City: 1.0, Metro: 0.85, Country: 0.6, Region: 0.3, Else: 0.1);
}
