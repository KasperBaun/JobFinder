using Jobmatch.Domain.Runs;
using Jobmatch.Features.Providers;
using Jobmatch.Features.Skillsets;
using Jobmatch.Pipeline.Ranking;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Pipeline.Stages;

/// <summary>Resolves a <see cref="RunPlan"/> from the request and the user's saved configuration.</summary>
public sealed class RunPlanner(UserContext ctx)
{
    public RunPlan Plan(SearchRequest req, IReadOnlyList<PortalConfig> allPortals)
    {
        if (!File.Exists(ctx.SkillsetPath))
            throw new InvalidRequestException("Set up your profile before running a search.");

        var ranking = RankingConfigLoader.Load(ctx.RankingPath);
        var requested = req.Providers is { Count: > 0 }
            ? new HashSet<string>(req.Providers, StringComparer.OrdinalIgnoreCase)
            : null;
        var enabled = allPortals
            .Where(p => p.Enabled)
            .Where(p => requested is null || requested.Contains(p.Name))
            .ToList();

        return new RunPlan(
            SkillsetParser.Load(ctx.SkillsetPath),
            ranking,
            allPortals,
            enabled,
            req.TopN ?? ranking.TopN,
            req.MinScore ?? ranking.MinScoreToInclude,
            DateTimeOffset.UtcNow);
    }
}
