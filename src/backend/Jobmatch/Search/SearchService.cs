using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.IO;
using Jobmatch.Llm;
using Jobmatch.Models;
using Jobmatch.Output;
using Jobmatch.Ranking;
using Jobmatch.Services;
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
    private readonly IMarksService _marks;

    public SearchService(UserContext ctx, IFileSystem fs, ILoggerFactory? loggerFactory = null, IMarksService? marks = null)
    {
        _ctx = ctx;
        _fs = fs;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _marks = marks ?? new MarksService(ctx);
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
            var examples = LoadExamples();
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

    // Curated examples/ files plus listings the user marked in previous runs
    // (with their "why" reasons). Curated wins on a (title, company) collision.
    internal IReadOnlyList<ExampleListing> LoadExamples()
    {
        var curated = ExamplesLoader.Load(_ctx.ExamplesDir);
        var marked = MarkedExamplesLoader.Load(_ctx.HistoryDir, _marks.LoadAll());
        if (marked.Count == 0) return curated;

        var seen = new HashSet<string>(
            curated.Select(e => $"{e.Title}|{e.Company}"), StringComparer.OrdinalIgnoreCase);
        var merged = new List<ExampleListing>(curated);
        merged.AddRange(marked.Where(m => seen.Add($"{m.Title}|{m.Company}")));
        return merged;
    }

    private RunPrep Prepare(SearchRequest req, IReadOnlyList<PortalConfig> allPortals)
    {
        if (!File.Exists(_ctx.SkillsetPath))
            throw new InvalidRequestException("Set up your profile before running a search.");

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
        var total = enabled.Count;
        if (total == 0) yield break;

        // Fetch every provider concurrently — they're independent I/O. Each result lands in a
        // fixed-position slot so `fetched` is reassembled in enabled-order once all complete,
        // keeping first-wins dedupe deterministic regardless of which provider returns first.
        var slots = new ProviderFetchResult[total];
        var channel = Channel.CreateUnbounded<SearchProgressEvent>();

        async Task FetchOne(int i)
        {
            var portal = enabled[i];
            var index = i + 1;
            await channel.Writer.WriteAsync(new ProviderRunningEvent(portal.Name, index, total), ct).ConfigureAwait(false);
            var (results, error) = await FetchSafe(portal, http, ct).ConfigureAwait(false);
            slots[i] = new ProviderFetchResult(portal.Name, results, error);
            await channel.Writer.WriteAsync(
                error is null
                    ? new ProviderDoneEvent(portal.Name, results.Count, index, total)
                    : new ProviderFailedEvent(portal.Name, error, index, total),
                ct).ConfigureAwait(false);
        }

        var pending = new Task[total];
        for (var i = 0; i < total; i++) pending[i] = FetchOne(i);

        async Task DrainToCompletion()
        {
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            finally { channel.Writer.Complete(); }
        }
        var completion = DrainToCompletion();

        await foreach (var evt in channel.Reader.ReadAllAsync().ConfigureAwait(false))
            yield return evt;

        await completion.ConfigureAwait(false);
        Collect(slots, fetched, statuses, rawByProvider);
    }

    private static void Collect(
        ProviderFetchResult[] slots,
        List<Listing> fetched,
        List<ProviderRunStatus> statuses,
        Dictionary<string, IReadOnlyList<Listing>> rawByProvider)
    {
        foreach (var slot in slots)
        {
            if (slot.Error is null)
            {
                fetched.AddRange(slot.Results);
                rawByProvider[slot.Name] = slot.Results;
                statuses.Add(new ProviderRunStatus(slot.Name, ProviderRunState.Ok, slot.Results.Count, null));
            }
            else
            {
                rawByProvider[slot.Name] = [];
                statuses.Add(new ProviderRunStatus(slot.Name, ProviderRunState.Failed, null, slot.Error));
            }
        }
    }

    /// <summary>One provider's fetch outcome, held in enabled-order so result assembly stays deterministic.</summary>
    private readonly record struct ProviderFetchResult(string Name, IReadOnlyList<Listing> Results, string? Error);

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
