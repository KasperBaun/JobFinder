using System.Runtime.CompilerServices;
using Jobmatch.Domain;
using Jobmatch.Search.Planning;
using Jobmatch.Search.Locations;
using Jobmatch.Infrastructure.Llm;
using Microsoft.Extensions.Logging;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Search.Judging;

/// <summary>
/// The optional AI re-rank layer: judge the listings that can still reach the shortlist, blend each
/// verdict into the keyword score, and repeat for whatever the reshuffle promoted.
/// </summary>
/// <remarks>
/// This owns the whole judging concern — the client's lifetime, the pass loop, and the blending
/// formula — so the orchestrator only has to decide whether AI is switched on. Every failure mode
/// degrades to keyword-only scoring rather than failing the run: the model file may be missing,
/// Ollama may not be running, a pass may return nothing.
/// </remarks>
public sealed class AiReview(string modelRootDir, ILoggerFactory loggers)
{
    /// <summary>
    /// Judges pass after pass until the shortlist is stable and fully judged, blending each pass's
    /// verdicts into <paramref name="scored"/> in place. One model load for the whole sequence.
    /// Yields a progress event per pass — the run is silent for seconds per verdict otherwise.
    /// </summary>
    public async IAsyncEnumerable<SearchProgressEvent> JudgeUntilShortlistStable(
        List<Match> scored,
        RunPlan plan,
        RadiusFilter? radius,
        IReadOnlyList<ExampleListing> examples,
        HttpClient http,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var llm = plan.Ranking.Llm;
        var judge = plan.Ranking.Judge;
        var planner = new JudgePlanner(plan.Ranking, plan.MinScore, plan.TopN, radius, judge.FirstPassBudget);
        var toJudge = planner.NextCandidates(scored);
        if (toJudge.Count == 0) yield break;

        ILlmClient? client = null;
        try
        {
            while (toJudge.Count > 0)
            {
                yield return new LlmJudgingEvent(toJudge.Count, Followup: planner.PassesHandedOut > 1);

                client ??= LlmClientFactory.Create(llm, modelRootDir, http, loggers);
                if (client is null) yield break;

                var verdicts = await JudgePass(scored, toJudge, client, plan.Skillset, examples, judge.Weight, ct)
                    .ConfigureAwait(false);
                // A pass that returned nothing means the model is unreachable, not that these
                // particular listings confused it — further passes would only burn the budget.
                if (verdicts == 0) yield break;

                toJudge = planner.NextCandidates(scored);
            }
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }

    private async Task<int> JudgePass(
        List<Match> scored,
        IReadOnlyList<Match> toJudge,
        ILlmClient client,
        Skillset skillset,
        IReadOnlyList<ExampleListing> examples,
        double weight,
        CancellationToken ct)
    {
        var judge = new LlmJudge(client, loggers.CreateLogger<LlmJudge>());
        var verdicts = await judge.JudgeAsync(toJudge, skillset, examples, ct).ConfigureAwait(false);

        var byId = new Dictionary<string, LlmVerdict>(StringComparer.Ordinal);
        foreach (var (match, verdict) in verdicts)
        {
            if (verdict is not null) byId[match.Listing.Id] = verdict;
        }
        if (byId.Count == 0) return 0;

        var blended = scored.Select(m => Blend(m, byId, weight)).ToList();
        scored.Clear();
        scored.AddRange(blended);
        return byId.Count;
    }

    internal static Match Blend(Match m, IReadOnlyDictionary<string, LlmVerdict> verdicts, double weight)
    {
        if (!verdicts.TryGetValue(m.Listing.Id, out var v)) return m;

        var reason = string.IsNullOrWhiteSpace(v.Reason) ? null : v.Reason;
        return m with
        {
            Score = Math.Clamp(weight * v.Score + (1 - weight) * m.Score, 0.0, 1.0),
            Reasoning = m.Reasoning with
            {
                Notes = reason is null ? m.Reasoning.Notes : $"{m.Reasoning.Notes} AI review: {v.Score:0.00} — {v.Reason}",
                LlmScore = v.Score,
                LlmReason = reason,
            },
        };
    }
}
