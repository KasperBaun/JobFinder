using Jobmatch.Infrastructure.Llm;

namespace Jobmatch.Search.Ranking;

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
    public JudgeConfig Judge { get; init; } = JudgeConfig.Default;
}

/// <summary>
/// What the AI judge may spend and how far its verdict moves a score. Read from the <c>llm:</c>
/// block of ranking.yml alongside <see cref="LlmConfig"/>, but ranking policy rather than plumbing:
/// changing these changes the shortlist, not how the model is reached.
/// </summary>
public sealed record JudgeConfig(
    int FirstPassBudget,   // verdicts the first pass may spend (0 = every eligible listing)
    double Weight)         // 0.0 = keyword-only, 1.0 = LLM-only
{
    public static JudgeConfig Default { get; } = new(FirstPassBudget: 50, Weight: 0.5);
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
