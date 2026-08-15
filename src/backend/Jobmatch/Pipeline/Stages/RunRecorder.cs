using Jobmatch.Domain;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.History;
using Jobmatch.Pipeline.Deduplication;
using Jobmatch.Pipeline.Output;
using Jobmatch.Infrastructure.Paths;
using Match = Jobmatch.Domain.Match;

namespace Jobmatch.Pipeline.Stages;

/// <summary>The outputs of a finished run, in the shapes each destination wants.</summary>
public sealed record RunResults(
    RunPlan Plan,
    IReadOnlyList<ProviderRunStatus> Statuses,
    IReadOnlyDictionary<string, IReadOnlyList<Listing>> RawByProvider,
    int FetchedCount,
    int DedupedCount,
    IReadOnlyList<DedupeGroup> DedupeMerges,
    IReadOnlyList<Match> Scored,
    IReadOnlyList<Match> Shortlist,
    IReadOnlyList<DroppedEntry> Dropped,
    ProbabilisticDedupeResult ProbabilisticDedupe);

/// <summary>
/// Everything a finished run leaves behind: the two report files the user reads, and the run record
/// the history views are built from. Separate from the orchestrator because what a run produces and
/// how it is recorded change for different reasons than how it is executed.
/// </summary>
public sealed class RunRecorder(UserContext ctx, IRunHistoryStore history)
{
    /// <summary>
    /// Writes the reports and the run record, and returns the shortlist projected to
    /// <see cref="ListingMatch"/> for the completion event.
    /// </summary>
    public IReadOnlyList<ListingMatch> Record(string runId, RunResults results)
    {
        var plan = results.Plan;

        JsonReportWriter.WriteMatches(results.Shortlist, ctx.RankedListingsPath);
        MarkdownReportWriter.WriteMatches(
            results.Shortlist,
            ctx.TopJobsPath,
            $"Top matches — {plan.Skillset.Name} — {plan.StartedAt:yyyy-MM-dd HH:mm} UTC");

        var displayNames = plan.AllPortals.ToDictionary(
            p => p.Name,
            p => string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName!,
            StringComparer.Ordinal);

        var shortlist = results.Shortlist
            .Select(m => RunRecordProjection.ToListingMatch(
                m, displayNames, RunRecordProjection.ToSightings(m, results.ProbabilisticDedupe, displayNames)))
            .ToList();

        // Persist the full RunDetail shape (without marks — those live in marks.json) so the
        // history-detail endpoint can deserialise this directly.
        history.Save(new RunDetail(
            RunId: runId,
            StartedAt: plan.StartedAt,
            Providers: results.Statuses,
            FetchedCount: results.FetchedCount,
            DedupedCount: results.DedupedCount,
            RankedCount: shortlist.Count,
            ShortlistCount: shortlist.Count,
            TopScore: shortlist.Count > 0 ? shortlist[0].Score : 0.0,
            GoodMarks: 0,
            Shortlist: shortlist,
            Marks: new Dictionary<string, string>(),
            Raw: [.. results.RawByProvider.Select(kvp =>
                new ProviderRaw(kvp.Key, kvp.Value.Select(RunRecordProjection.ToRawListing).ToList()))],
            DedupeMerges: results.DedupeMerges,
            Scored: [.. results.Scored.Select(m => RunRecordProjection.ToScoredEntry(m, displayNames))],
            Dropped: results.Dropped,
            PossibleDuplicates: results.ProbabilisticDedupe.PossibleDuplicates.Count > 0
                ? results.ProbabilisticDedupe.PossibleDuplicates
                : null));

        return shortlist;
    }
}
