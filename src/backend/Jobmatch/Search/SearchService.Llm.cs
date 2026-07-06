using Jobmatch.Llm;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    private async Task<IReadOnlyList<Match>> JudgeAndBlend(
        IReadOnlyList<Match> scored,
        Skillset skillset,
        IReadOnlyList<ExampleListing> examples,
        LlmConfig llmConfig,
        int topN,
        HttpClient http,
        CancellationToken ct)
    {
        var client = LlmClientFactory.Create(llmConfig, _ctx.RootDir, http, _loggerFactory);
        if (client is null) return scored;
        try
        {
            var ordered = scored.OrderByDescending(m => m.Score).ToList();
            var toJudge = ordered.Take(topN).ToList();
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
                        Reasoning = m.Reasoning with { Notes = notes },
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
