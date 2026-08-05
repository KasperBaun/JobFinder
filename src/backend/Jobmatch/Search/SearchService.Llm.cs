using Jobmatch.Geo;
using Jobmatch.Llm;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    /// <summary>
    /// The judge budget (llm.top_n) buys verdicts only for listings that can still reach the
    /// shortlist. Spending it on the raw keyword top-N wastes calls on listings the hard
    /// filters discard moments later, which leaves genuine shortlist entries unjudged and
    /// silently keyword-scored. Score-dependent drops are excluded — they are settled from the
    /// blended score, so they cannot be known yet.
    /// </summary>
    internal static List<Match> SelectJudgeCandidates(
        IReadOnlyList<Match> scored,
        RankingConfig ranking,
        RadiusFilter? radius,
        int topN)
    {
        var eligible = scored
            .Where(m => ClassifyScoreIndependentDrop(m, ranking, radius) is null)
            .OrderByDescending(m => m.Score);
        return topN <= 0 ? eligible.ToList() : eligible.Take(topN).ToList();
    }

    private async Task<IReadOnlyList<Match>> JudgeAndBlend(
        IReadOnlyList<Match> scored,
        IReadOnlyList<Match> toJudge,
        Skillset skillset,
        IReadOnlyList<ExampleListing> examples,
        LlmConfig llmConfig,
        HttpClient http,
        CancellationToken ct)
    {
        if (toJudge.Count == 0) return scored;

        var client = LlmClientFactory.Create(llmConfig, _ctx.RootDir, http, _loggerFactory);
        if (client is null) return scored;
        try
        {
            var judge = new LlmJudge(client, _loggerFactory.CreateLogger<LlmJudge>());
            var verdicts = await judge.JudgeAsync(toJudge, skillset, examples, ct).ConfigureAwait(false);

            var verdictById = verdicts.ToDictionary(v => v.Match.Listing.Id, v => v.Verdict);
            var w = llmConfig.Weight;
            var blended = new List<Match>(scored.Count);
            foreach (var m in scored)
            {
                if (verdictById.TryGetValue(m.Listing.Id, out var v) && v is not null)
                {
                    var newScore = Math.Clamp(w * v.Score + (1 - w) * m.Score, 0.0, 1.0);
                    var notes = string.IsNullOrWhiteSpace(v.Reason)
                        ? m.Reasoning.Notes
                        : $"{m.Reasoning.Notes} AI review: {v.Score:0.00} — {v.Reason}";
                    blended.Add(m with
                    {
                        Score = newScore,
                        Reasoning = m.Reasoning with
                        {
                            Notes = notes,
                            LlmScore = v.Score,
                            LlmReason = string.IsNullOrWhiteSpace(v.Reason) ? null : v.Reason,
                        },
                    });
                }
                else
                {
                    blended.Add(m);
                }
            }
            return blended;
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
