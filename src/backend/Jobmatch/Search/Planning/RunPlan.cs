using Jobmatch.Domain;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Ranking;

namespace Jobmatch.Search.Planning;

/// <summary>
/// Everything a single run is settled on before it fetches anything: the profile to score against,
/// the ranking config, which sources are in play, and the run-level knobs the request may override.
/// Resolved once so no stage re-reads config mid-run and reaches a different answer.
/// </summary>
public sealed record RunPlan(
    Skillset Skillset,
    RankingConfig Ranking,
    IReadOnlyList<PortalConfig> AllPortals,
    IReadOnlyList<PortalConfig> Enabled,
    int TopN,
    double MinScore,
    DateTimeOffset StartedAt);
