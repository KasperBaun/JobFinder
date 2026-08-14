using Jobmatch.Domain;
using Jobmatch.Pipeline.Geo;
using Jobmatch.Pipeline.Ranking;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Pipeline;

public sealed partial class SearchService
{
    /// <summary>
    /// Hands out one judging pass at a time until every listing on the shortlist has been offered
    /// to the judge, or the budget runs out.
    /// <para>
    /// Judging rewrites scores, which reorders, so the shortlist taken after blending is not the
    /// set that was judged before it — listings promoted by the reshuffle would show up unjudged
    /// and keyword-scored. The first pass spends <c>llm.top_n</c> on the keyword-best eligible
    /// listings; every later pass judges only the shortlist members that no pass has attempted yet.
    /// </para>
    /// <para>
    /// Two independent stops, because each verdict costs seconds of local inference and the stream
    /// is silent while it runs: a verdict budget of <c>llm.top_n + top_n</c> (the reserve is exactly
    /// one full replacement of the shortlist) and <see cref="MaxPasses"/> passes. Termination is
    /// unconditional — a pass only runs when it has at least one candidate, so the budget strictly
    /// decreases. Neither stop guarantees a fully judged shortlist; they guarantee the run ends.
    /// </para>
    /// </summary>
    internal sealed class JudgePlanner(
        RankingConfig ranking,
        double minScore,
        int topN,
        RadiusFilter? radius,
        int firstPassN)
    {
        internal const int MaxPasses = 4;

        private readonly HashSet<string> _attempted = new(StringComparer.Ordinal);
        private int _remaining = Budget(firstPassN, topN);

        /// <summary>Passes handed out so far — 1 while the first pass is judging.</summary>
        internal int Pass { get; private set; }

        internal int Remaining => _remaining;

        /// <summary>Candidates for the next pass against the scores as they now stand; empty when done.</summary>
        internal List<Match> Next(IReadOnlyList<Match> scored)
        {
            if (Pass >= MaxPasses || _remaining <= 0) return [];

            var candidates = Pass == 0
                ? SelectJudgeCandidates(scored, ranking, radius, firstPassN)
                : SelectUnjudgedShortlist(scored, ranking, minScore, topN, radius, _attempted);
            if (candidates.Count > _remaining) candidates = [.. candidates.Take(_remaining)];
            if (candidates.Count == 0) return [];

            foreach (var m in candidates) _attempted.Add(m.Listing.Id);
            _remaining -= candidates.Count;
            Pass++;
            return candidates;
        }

        // llm.top_n = 0 already means "judge every eligible listing", so the first pass covers the
        // whole shortlist and there is nothing to reserve.
        private static int Budget(int firstPassN, int topN)
            => firstPassN <= 0 ? int.MaxValue : firstPassN + Math.Max(topN, 0);
    }
}
