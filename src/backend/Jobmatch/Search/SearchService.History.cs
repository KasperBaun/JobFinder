using System.Text.Json;
using Jobmatch.Deduplication;
using Jobmatch.Models;
using Jobmatch.Output;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = Json.JobmatchJsonOptions.Indented;

    /// <summary>Writes the ranked-JSON + markdown reports, builds the rich history sections, persists
    /// the history entry, and returns the shortlist projected to <see cref="ListingMatch"/> for the
    /// completion event.</summary>
    private IReadOnlyList<ListingMatch> WriteReportsAndHistory(
        string runId,
        RunPrep prep,
        IReadOnlyList<ProviderRunStatus> statuses,
        IReadOnlyDictionary<string, IReadOnlyList<Listing>> rawByProvider,
        IReadOnlyList<Listing> fetched,
        IReadOnlyList<Listing> deduped,
        IReadOnlyList<DedupeGroup> dedupeMerges,
        IReadOnlyList<Match> scoredAll,
        IReadOnlyList<Match> shortlist,
        IReadOnlyList<DroppedEntry> dropped)
    {
        JsonReportWriter.WriteMatches(shortlist, _ctx.RankedListingsPath);
        var mdTitle = $"Top matches — {prep.Skillset.Name} — {prep.StartedAt:yyyy-MM-dd HH:mm} UTC";
        MarkdownReportWriter.WriteMatches(shortlist, _ctx.TopJobsPath, mdTitle);

        var portalDisplayNames = prep.AllPortals.ToDictionary(
            p => p.Name,
            p => string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName!,
            StringComparer.Ordinal);
        var listingMatches = shortlist.Select(m => ToListingMatch(m, portalDisplayNames)).ToList();

        var rawSection = rawByProvider
            .Select(kvp => new ProviderRaw(kvp.Key, kvp.Value.Select(ToRawListing).ToList()))
            .ToList();
        var scoredSection = scoredAll.Select(m => ToScoredEntry(m, portalDisplayNames)).ToList();

        WriteHistory(
            runId,
            prep.StartedAt,
            statuses,
            fetched.Count,
            deduped.Count,
            shortlist.Count,
            listingMatches,
            rawSection,
            dedupeMerges,
            scoredSection,
            dropped);

        return listingMatches;
    }

    private void WriteHistory(
        string runId,
        DateTimeOffset startedAt,
        IReadOnlyList<ProviderRunStatus> providers,
        int fetchedCount,
        int dedupedCount,
        int shortlistCount,
        IReadOnlyList<ListingMatch> shortlist,
        IReadOnlyList<ProviderRaw> raw,
        IReadOnlyList<DedupeGroup> dedupeMerges,
        IReadOnlyList<ScoredEntry> scored,
        IReadOnlyList<DroppedEntry> dropped)
    {
        Directory.CreateDirectory(_ctx.HistoryDir);

        var topScore = shortlist.Count > 0 ? shortlist[0].Score : 0.0;

        // Persist the full RunDetail shape (without marks — those live in marks.json) so the
        // history-detail endpoint can deserialise this directly.
        var detail = new RunDetail(
            RunId: runId,
            StartedAt: startedAt,
            Providers: providers,
            FetchedCount: fetchedCount,
            DedupedCount: dedupedCount,
            RankedCount: shortlistCount,
            ShortlistCount: shortlistCount,
            TopScore: topScore,
            GoodMarks: 0,
            Shortlist: shortlist,
            Marks: new Dictionary<string, string>(),
            Raw: raw,
            DedupeMerges: dedupeMerges,
            Scored: scored,
            Dropped: dropped);

        var path = Path.Combine(_ctx.HistoryDir, $"{runId}.json");
        var json = JsonSerializer.Serialize(detail, HistoryJsonOptions);
        File.WriteAllText(path, json);
    }
}
