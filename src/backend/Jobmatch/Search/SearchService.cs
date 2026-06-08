using System.Runtime.CompilerServices;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.IO;
using Jobmatch.Llm;
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

    /// <summary>Run with a caller-supplied run id (used by the background job so the id is known before execution).</summary>
    IAsyncEnumerable<SearchProgressEvent> RunAsync(SearchRequest req, string runId, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates a single search run: load configs → fetch from each enabled portal → dedupe →
/// rank → write reports → persist a history entry. Yields progress events so callers (CLI or
/// GUI) can stream a live view of the run.
/// </summary>
public sealed partial class SearchService : ISearchService
{
    private readonly UserContext _ctx;
    private readonly IFileSystem _fs;
    private readonly ILoggerFactory _loggerFactory;

    public SearchService(UserContext ctx, IFileSystem fs, ILoggerFactory? loggerFactory = null)
    {
        _ctx = ctx;
        _fs = fs;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        CancellationToken ct = default)
        => RunAsync(req, BuildRunId(DateTimeOffset.UtcNow), ct);

    public async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        string runId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "portals.json");
        var catalog = PortalCatalogLoader.Load(catalogPath);
        var state = ProviderStateLoader.LoadOrEmpty(_ctx.ProviderStatePath);
        var allPortals = ProviderStateMerger.Merge(catalog, state);

        await foreach (var evt in RunAsync(req, runId, allPortals, ct).ConfigureAwait(false))
            yield return evt;
    }

    internal IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        IReadOnlyList<PortalConfig> allPortals,
        CancellationToken ct = default)
        => RunAsync(req, BuildRunId(DateTimeOffset.UtcNow), allPortals, ct);

    internal async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        string runId,
        IReadOnlyList<PortalConfig> allPortals,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prep = Prepare(req, allPortals);
        yield return new StartedEvent(runId, prep.Enabled.Count);

        using var http = new HttpClient();
        var statuses = new List<ProviderRunStatus>(prep.Enabled.Count);
        var rawByProvider = new Dictionary<string, IReadOnlyList<Listing>>(StringComparer.Ordinal);
        var fetched = new List<Listing>();
        await foreach (var evt in FetchAll(prep.Enabled, http, statuses, rawByProvider, fetched, ct).ConfigureAwait(false))
            yield return evt;

        var dedupeResult = Deduper.Deduplicate(fetched);
        var deduped = dedupeResult.Deduped;
        yield return new DedupeEvent(deduped.Count);
        JsonReportWriter.WriteListings(deduped, _ctx.AllListingsPath);

        var scoredAll = Ranker.Score(deduped, prep.Skillset, prep.Ranking);

        // Optional LLM re-rank layer. Picks the top-N from the keyword ranker, asks the model to
        // score each against the user's skillset + curated examples, then blends LLM and keyword
        // scores. Falls back transparently to keyword-only when the model can't be loaded (file
        // missing, Ollama not running, etc.). Sequential — gemma 3 4B on CPU does ~1-3 sec per call,
        // so judging the default top 50 takes ~1-2 minutes; SSE stream stays open but silent.
        if (prep.Ranking.Llm.Enabled)
        {
            var examples = ExamplesLoader.Load(_ctx.ExamplesDir);
            var llmTopN = prep.Ranking.Llm.TopN <= 0 ? scoredAll.Count : Math.Min(prep.Ranking.Llm.TopN, scoredAll.Count);
            yield return new LlmJudgingEvent(llmTopN);
            scoredAll = await JudgeAndBlend(scoredAll, prep.Skillset, examples, prep.Ranking.Llm, llmTopN, http, ct).ConfigureAwait(false);
        }

        var (shortlist, dropped) = BuildShortlist(scoredAll, prep.Ranking, prep.MinScore, prep.TopN);
        yield return new RankEvent(shortlist.Count, shortlist.Count > 0 ? shortlist[0].Score : 0.0);

        var listingMatches = WriteReportsAndHistory(
            runId, prep, statuses, rawByProvider, fetched, deduped, dedupeResult.Merges, scoredAll, shortlist, dropped);
        yield return new CompleteEvent(runId, listingMatches);
    }

    private RunPrep Prepare(SearchRequest req, IReadOnlyList<PortalConfig> allPortals)
    {
        var ranking = RankingConfigLoader.Load(_ctx.RankingPath);
        var requested = req.Providers is { Count: > 0 }
            ? new HashSet<string>(req.Providers, StringComparer.OrdinalIgnoreCase)
            : null;
        var enabled = allPortals
            .Where(p => p.Enabled)
            .Where(p => requested is null || requested.Contains(p.Name))
            .ToList();
        return new RunPrep(
            SkillsetParser.Load(_ctx.SkillsetPath),
            ranking,
            allPortals,
            enabled,
            req.TopN ?? ranking.TopN,
            req.MinScore ?? ranking.MinScoreToInclude,
            DateTimeOffset.UtcNow);
    }

    private async IAsyncEnumerable<SearchProgressEvent> FetchAll(
        IReadOnlyList<PortalConfig> enabled,
        HttpClient http,
        List<ProviderRunStatus> statuses,
        Dictionary<string, IReadOnlyList<Listing>> rawByProvider,
        List<Listing> fetched,
        [EnumeratorCancellation] CancellationToken ct)
    {
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
                statuses.Add(new ProviderRunStatus(portal.Name, ProviderRunState.Ok, results.Count, null));
                yield return new ProviderDoneEvent(portal.Name, results.Count, index, enabled.Count);
            }
            else
            {
                rawByProvider[portal.Name] = [];
                statuses.Add(new ProviderRunStatus(portal.Name, ProviderRunState.Failed, null, error));
                yield return new ProviderFailedEvent(portal.Name, error, index, enabled.Count);
            }
        }
    }

    /// <summary>Resolved inputs for a single run — config + the filtered portal set + run-level knobs.</summary>
    private readonly record struct RunPrep(
        Skillset Skillset,
        RankingConfig Ranking,
        IReadOnlyList<PortalConfig> AllPortals,
        List<PortalConfig> Enabled,
        int TopN,
        double MinScore,
        DateTimeOffset StartedAt);

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

    private static string BuildRunId(DateTimeOffset startedAt) => Jobmatch.Jobs.RunId.New(startedAt);
}
