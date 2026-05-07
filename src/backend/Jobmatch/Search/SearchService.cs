using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.IO;
using Jobmatch.Models;
using Jobmatch.Output;
using Jobmatch.Ranking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public interface ISearchService
{
    IAsyncEnumerable<SearchProgressEvent> RunAsync(SearchRequest req, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates a single search run: load configs → fetch from each enabled portal → dedupe →
/// rank → write reports → persist a history entry. Yields progress events so callers (CLI or
/// GUI) can stream a live view of the run.
/// </summary>
public sealed class SearchService : ISearchService
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = Json.JobmatchJsonOptions.Indented;

    private readonly UserContext _ctx;
    private readonly IFileSystem _fs;
    private readonly ILoggerFactory _loggerFactory;

    public SearchService(UserContext ctx, IFileSystem fs, ILoggerFactory? loggerFactory = null)
    {
        _ctx = ctx;
        _fs = fs;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "portals.json");
        var catalog = PortalCatalogLoader.Load(catalogPath);
        var state = ProviderStateLoader.LoadOrEmpty(_ctx.ProviderStatePath);
        var allPortals = ProviderStateMerger.Merge(catalog, state);

        await foreach (var evt in RunAsync(req, allPortals, ct).ConfigureAwait(false))
            yield return evt;
    }

    internal async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        IReadOnlyList<PortalConfig> allPortals,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skillset = SkillsetParser.Load(_ctx.SkillsetPath);
        var ranking = RankingConfigLoader.Load(_ctx.RankingPath);

        var requested = req.Providers is { Count: > 0 }
            ? new HashSet<string>(req.Providers, StringComparer.OrdinalIgnoreCase)
            : null;

        var enabled = allPortals
            .Where(p => p.Enabled)
            .Where(p => requested is null || requested.Contains(p.Name))
            .ToList();

        var startedAt = DateTimeOffset.UtcNow;
        var runId = BuildRunId(startedAt);

        yield return new StartedEvent(enabled.Count);

        using var http = new HttpClient();
        var statuses = new List<ProviderRunStatus>(enabled.Count);
        var rawByProvider = new Dictionary<string, IReadOnlyList<Listing>>(StringComparer.Ordinal);
        var fetched = new List<Listing>();

        for (var i = 0; i < enabled.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var portal = enabled[i];
            var index = i + 1;

            yield return new ProviderRunningEvent(portal.Name, index, enabled.Count);

            var (results, error) = await FetchSafe(portal, http, ct).ConfigureAwait(false);
            if (error is null)
            {
                fetched.AddRange(results);
                rawByProvider[portal.Name] = results;
                statuses.Add(new ProviderRunStatus(portal.Name, "ok", results.Count, null));
                yield return new ProviderDoneEvent(portal.Name, results.Count, index, enabled.Count);
            }
            else
            {
                rawByProvider[portal.Name] = [];
                statuses.Add(new ProviderRunStatus(portal.Name, "failed", null, error));
                yield return new ProviderFailedEvent(portal.Name, error, index, enabled.Count);
            }
        }

        var dedupeResult = Deduper.Deduplicate(fetched);
        var deduped = dedupeResult.Deduped;
        yield return new DedupeEvent(deduped.Count);

        JsonReportWriter.WriteListings(deduped, _ctx.AllListingsPath);

        var topN = req.TopN ?? ranking.TopN;
        var minScore = req.MinScore ?? ranking.MinScoreToInclude;

        var scoredAll = Ranker.Score(deduped, skillset, ranking);

        var dropped = new List<DroppedEntry>();
        var passed = new List<Match>();
        foreach (var m in scoredAll)
        {
            var reason = ClassifyDrop(m, ranking, minScore);
            if (reason is null)
            {
                passed.Add(m);
            }
            else
            {
                dropped.Add(BuildDroppedEntry(m, reason.Value.Reason, reason.Value.Context));
            }
        }

        var ordered = passed.OrderByDescending(m => m.Score).ToList();
        var shortlist = ordered.Take(topN).ToList();
        for (var i = topN; i < ordered.Count; i++)
        {
            var m = ordered[i];
            dropped.Add(BuildDroppedEntry(m, "beyond_top_n", $"rank {i + 1} of {ordered.Count} (top {topN} taken)"));
        }

        var topScore = shortlist.Count > 0 ? shortlist[0].Score : 0.0;
        yield return new RankEvent(shortlist.Count, topScore);

        JsonReportWriter.WriteMatches(shortlist, _ctx.RankedListingsPath);
        var mdTitle = $"Top matches — {skillset.Name} — {startedAt:yyyy-MM-dd HH:mm} UTC";
        MarkdownReportWriter.WriteMatches(shortlist, _ctx.TopJobsPath, mdTitle);

        var listingMatches = shortlist.Select(ToListingMatch).ToList();

        var rawSection = rawByProvider
            .Select(kvp => new ProviderRaw(kvp.Key, kvp.Value.Select(ToRawListing).ToList()))
            .ToList();
        var scoredSection = scoredAll.Select(ToScoredEntry).ToList();

        WriteHistory(
            runId,
            startedAt,
            statuses,
            fetched.Count,
            deduped.Count,
            shortlist.Count,
            listingMatches,
            rawSection,
            dedupeResult.Merges,
            scoredSection,
            dropped);

        yield return new CompleteEvent(runId, listingMatches);
    }

    private async Task<(IReadOnlyList<Listing> Results, string? Error)> FetchSafe(
        PortalConfig portal,
        HttpClient http,
        CancellationToken ct)
    {
        try
        {
            var logger = _loggerFactory.CreateLogger($"Adapter.{portal.Name}");
            var adapter = AdapterFactory.Create(portal, http, logger, _ctx.ImportsDir, _fs);
            if (adapter is null)
            {
                return (Array.Empty<Listing>(), $"unsupported portal type '{portal.Type}'");
            }

            var results = await adapter.FetchAsync(ct).ConfigureAwait(false);
            return (results, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (Array.Empty<Listing>(), ex.Message);
        }
    }

    private static string BuildRunId(DateTimeOffset startedAt)
    {
        var stamp = startedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{stamp}-{suffix}";
    }

    private static ListingMatch ToListingMatch(Match match)
    {
        var l = match.Listing;
        return new ListingMatch(
            Id: l.Id,
            Portal: l.Portal,
            Title: l.Title,
            Company: l.Company,
            Location: l.Location,
            RemoteMode: l.RemoteMode.ToString().ToLowerInvariant(),
            Url: l.Url.ToString(),
            PostedAt: l.PostedAt,
            Score: match.Score,
            Reasoning: match.Reasoning.Notes,
            PrimaryStackHits: match.Reasoning.PrimaryStackHits,
            SecondaryStackHits: match.Reasoning.SecondaryStackHits);
    }

    private static RawListing ToRawListing(Listing l) => new(
        Id: l.Id,
        Title: l.Title,
        Company: l.Company,
        Location: l.Location,
        Url: l.Url.ToString(),
        PostedAt: l.PostedAt);

    private static ScoredEntry ToScoredEntry(Match m) => new(
        Id: m.Listing.Id,
        Title: m.Listing.Title,
        Company: m.Listing.Company,
        Location: m.Listing.Location,
        Url: m.Listing.Url.ToString(),
        PostedAt: m.Listing.PostedAt,
        Portal: m.Listing.Portal,
        Score: m.Score,
        Breakdown: m.Breakdown,
        PrimaryStackHits: m.Reasoning.PrimaryStackHits,
        SecondaryStackHits: m.Reasoning.SecondaryStackHits);

    private static DroppedEntry BuildDroppedEntry(Match m, string reason, string? context) => new(
        Id: m.Listing.Id,
        Title: m.Listing.Title,
        Company: m.Listing.Company,
        Score: m.Score,
        Reason: reason,
        Context: context);

    /// <summary>
    /// Classifies why a scored match would be excluded from the shortlist. Order of
    /// precedence: above_max_age (hard cutoff) → missing_required_primary →
    /// disqualifier → below_min_score. Returns null if the match should pass to
    /// shortlist consideration. beyond_top_n is decided after sorting, not here.
    /// </summary>
    private static (string Reason, string? Context)? ClassifyDrop(Match m, RankingConfig ranking, double minScore)
    {
        if (ranking.MaxAgeDays is int maxAge && m.Listing.PostedAt is DateTimeOffset posted)
        {
            var ageDays = (DateTimeOffset.UtcNow - posted).TotalDays;
            if (ageDays > maxAge)
            {
                return ("above_max_age", $"posted {(int)Math.Round(ageDays)} days ago, max {maxAge}");
            }
        }

        if (ranking.RequirePrimaryStackHit && m.Reasoning.PrimaryStackHits.Count == 0)
        {
            return ("missing_required_primary", "no primary-stack keyword matched in title or description");
        }

        if (m.Reasoning.DisqualifierHits.Count > 0)
        {
            return ("disqualifier", $"matched disqualifier: {string.Join(", ", m.Reasoning.DisqualifierHits)}");
        }

        if (m.Score < minScore)
        {
            return ("below_min_score", $"score {m.Score:0.00} below threshold {minScore:0.00}");
        }

        return null;
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
