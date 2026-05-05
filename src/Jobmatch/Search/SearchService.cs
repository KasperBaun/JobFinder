using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.Models;
using Jobmatch.Output;
using Jobmatch.Ranking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

/// <summary>
/// Orchestrates a single search run: load configs → fetch from each enabled portal → dedupe →
/// rank → write reports → persist a history entry. Yields progress events so callers (CLI or
/// GUI) can stream a live view of the run.
/// </summary>
public sealed class SearchService
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly UserContext _ctx;
    private readonly ILoggerFactory _loggerFactory;

    public SearchService(UserContext ctx, ILoggerFactory? loggerFactory = null)
    {
        _ctx = ctx;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skillset = SkillsetParser.Load(_ctx.SkillsetPath);
        var allPortals = PortalConfigLoader.Load(_ctx.PortalsPath);
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
                statuses.Add(new ProviderRunStatus(portal.Name, "ok", results.Count, null));
                yield return new ProviderDoneEvent(portal.Name, results.Count, index, enabled.Count);
            }
            else
            {
                statuses.Add(new ProviderRunStatus(portal.Name, "failed", null, error));
                yield return new ProviderFailedEvent(portal.Name, error, index, enabled.Count);
            }
        }

        var deduped = Deduper.Deduplicate(fetched);
        yield return new DedupeEvent(deduped.Count);

        JsonReportWriter.WriteListings(deduped, _ctx.AllListingsPath);

        var topN = req.TopN ?? ranking.TopN;
        var minScore = req.MinScore ?? ranking.MinScoreToInclude;

        var scored = Ranker.Score(deduped, skillset, ranking);
        var shortlist = scored
            .Where(m => m.Score >= minScore)
            .OrderByDescending(m => m.Score)
            .Take(topN)
            .ToList();

        var topScore = shortlist.Count > 0 ? shortlist[0].Score : 0.0;
        yield return new RankEvent(shortlist.Count, topScore);

        JsonReportWriter.WriteMatches(shortlist, _ctx.RankedListingsPath);
        var mdTitle = $"Top matches — {skillset.Name} — {startedAt:yyyy-MM-dd HH:mm} UTC";
        MarkdownReportWriter.WriteMatches(shortlist, _ctx.TopJobsPath, mdTitle);

        var listingMatches = shortlist.Select(ToListingMatch).ToList();

        WriteHistory(runId, startedAt, statuses, fetched.Count, deduped.Count, shortlist.Count, listingMatches);

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
            var adapter = AdapterFactory.Create(portal, http, logger, _ctx.ImportsDir);
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

    private void WriteHistory(
        string runId,
        DateTimeOffset startedAt,
        IReadOnlyList<ProviderRunStatus> providers,
        int fetchedCount,
        int dedupedCount,
        int shortlistCount,
        IReadOnlyList<ListingMatch> shortlist)
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
            Marks: new Dictionary<string, string>());

        var path = Path.Combine(_ctx.HistoryDir, $"{runId}.json");
        var json = JsonSerializer.Serialize(detail, HistoryJsonOptions);
        File.WriteAllText(path, json);
    }
}
