using System.Runtime.CompilerServices;
using Jobmatch.Domain;
using Jobmatch.Pipeline.Geo;
using Jobmatch.Pipeline.Llm;
using Jobmatch.Pipeline.Ranking;
using Microsoft.Extensions.Logging;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Pipeline;

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

    /// <summary>The shortlist as the blended scores now stand, minus everything already offered to
    /// the judge — i.e. exactly the entries that would render as "not reviewed by AI".</summary>
    internal static List<Match> SelectUnjudgedShortlist(
        IReadOnlyList<Match> scored,
        RankingConfig ranking,
        double minScore,
        int topN,
        RadiusFilter? radius,
        IReadOnlySet<string> attempted)
    {
        var (shortlist, _) = BuildShortlist(scored, ranking, minScore, topN, radius);
        return [.. shortlist.Where(m => !attempted.Contains(m.Listing.Id))];
    }

    /// <summary>
    /// Judges pass after pass until the shortlist is stable and fully judged, blending each pass's
    /// verdicts into <paramref name="scored"/> in place. One model load for the whole sequence.
    /// Yields a progress event per pass — the run is silent for seconds per verdict otherwise.
    /// </summary>
    private async IAsyncEnumerable<SearchProgressEvent> JudgeUntilShortlistStable(
        List<Match> scored,
        RunPrep prep,
        RadiusFilter? radius,
        HttpClient http,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var llm = prep.Ranking.Llm;
        var planner = new JudgePlanner(prep.Ranking, prep.MinScore, prep.TopN, radius, llm.TopN);
        var toJudge = planner.Next(scored);
        if (toJudge.Count == 0) yield break;

        var examples = LoadExamples();
        ILlmClient? client = null;
        try
        {
            while (toJudge.Count > 0)
            {
                yield return new LlmJudgingEvent(toJudge.Count, Followup: planner.Pass > 1);

                client ??= LlmClientFactory.Create(llm, _ctx.RootDir, http, _loggerFactory);
                if (client is null) yield break;

                var verdicts = await JudgePass(scored, toJudge, client, prep.Skillset, examples, llm.Weight, ct)
                    .ConfigureAwait(false);
                // A pass that returned nothing means the model is unreachable, not that these
                // particular listings confused it — further passes would only burn the budget.
                if (verdicts == 0) yield break;

                toJudge = planner.Next(scored);
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
        var judge = new LlmJudge(client, _loggerFactory.CreateLogger<LlmJudge>());
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

    private static Match Blend(Match m, IReadOnlyDictionary<string, LlmVerdict> verdicts, double weight)
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
