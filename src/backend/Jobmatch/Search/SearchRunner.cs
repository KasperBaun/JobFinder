using System.Runtime.CompilerServices;
using Jobmatch.Domain;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Applications;
using Jobmatch.Features.History;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Fetching;
using Jobmatch.Search.Deduplication;
using Jobmatch.Search.Locations;
using Jobmatch.Search.Ranking;
using Jobmatch.Search.Judging;
using Jobmatch.Search.Planning;
using Jobmatch.Search.Recording;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Paths;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Search;

/// <summary>
/// Runs one search: plan → fetch → dedupe → rank → judge → shortlist → record. Each phase is a folder
/// beside this file; this sequences them and yields the progress events a caller streams to the GUI.
/// </summary>
public sealed class SearchRunner : ISearchRunner
{
    private readonly IProviderCatalog _catalog;
    private readonly RunPlanner _planner;
    private readonly ProviderFetch _fetch;
    private readonly AiReview _review;
    private readonly ExampleSet _examples;
    private readonly RunRecorder _recorder;
    private readonly Gazetteer? _gazetteer;

    // A single source may paginate and body-enrich hundreds of listings; without a ceiling one
    // slow host holds up the whole run (every fetch is awaited under Task.WhenAll). This budget
    // bounds a source end-to-end — a straggler is abandoned and surfaced as a failed provider so
    // the run proceeds on the sources that returned. The parameter is a test seam; production
    // always uses the default.
    private static readonly TimeSpan DefaultPerSourceTimeout = TimeSpan.FromSeconds(120);

    public SearchRunner(
        UserContext ctx,
        IProviderCatalog catalog,
        IRunHistoryStore history,
        IFileSystem fs,
        ILoggerFactory? loggerFactory = null,
        IMarksService? marks = null,
        TimeSpan? perSourceTimeout = null,
        Gazetteer? gazetteer = null)
    {
        var loggers = loggerFactory ?? NullLoggerFactory.Instance;
        _catalog = catalog;
        _planner = new RunPlanner(ctx);
        _fetch = new ProviderFetch(
            ctx.ImportsDir, fs, loggers, perSourceTimeout ?? DefaultPerSourceTimeout);
        _review = new AiReview(ctx.RootDir, loggers);
        _examples = new ExampleSet(ctx, history, marks ?? new MarksService(ctx));
        _recorder = new RunRecorder(ctx, history);
        _gazetteer = gazetteer;
    }

    public IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        CancellationToken ct = default)
        => RunAsync(req, RunId.New(DateTimeOffset.UtcNow), ct);

    public IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        string runId,
        CancellationToken ct = default)
        // The catalog is the shipped sources plus the ones the user added themselves (R-090/R-119).
        // Composing this by hand here is what once made a user-added source appear on the providers
        // page yet never be fetched by a run.
        => RunAsync(req, runId, _catalog.Effective(), ct);

    internal IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        IReadOnlyList<PortalConfig> allPortals,
        CancellationToken ct = default)
        => RunAsync(req, RunId.New(DateTimeOffset.UtcNow), allPortals, ct);

    internal async IAsyncEnumerable<SearchProgressEvent> RunAsync(
        SearchRequest req,
        string runId,
        IReadOnlyList<PortalConfig> allPortals,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plan = _planner.Plan(req, allPortals);
        yield return new StartedEvent(runId, plan.Enabled.Count);

        using var clients = new SearchHttpClients();

        FetchOutcome fetched = null!;
        await foreach (var evt in _fetch.FetchAll(plan.Enabled, clients.Fetch, o => fetched = o, ct).ConfigureAwait(false))
            yield return evt;

        var places = _gazetteer ?? Gazetteer.LoadBundled();
        var unique = DuplicateMerger.Merge(fetched.Listings, places);
        yield return new DedupeEvent(unique.Deduped.Count);
        _recorder.WriteLonglist(unique.Deduped);

        var scored = Ranker.Score(unique.Deduped, plan.Skillset, plan.Ranking).ToList();
        var radiusFilter = RadiusFilter.Create(plan.Skillset, places);

        // Optional AI re-rank layer. Falls back transparently to keyword-only scoring whenever the
        // model can't be reached, so an unavailable model costs ranking quality, never the run.
        if (plan.Ranking.Llm.Enabled)
        {
            var judgeEvents = _review.JudgeUntilShortlistStable(
                scored, plan, radiusFilter, _examples.Load(), clients.Judge, ct);
            await foreach (var evt in judgeEvents.ConfigureAwait(false))
                yield return evt;
        }

        var (shortlist, dropped) = ShortlistBuilder.BuildShortlist(
            scored, plan.Ranking, plan.MinScore, plan.TopN, radiusFilter);
        yield return new RankEvent(shortlist.Count, shortlist.Count > 0 ? shortlist[0].Score : 0.0);

        var listingMatches = _recorder.Record(runId, new RunResults(
            Plan: plan,
            Statuses: fetched.Statuses,
            RawByProvider: fetched.ByProvider,
            FetchedCount: fetched.Listings.Count,
            DedupedCount: unique.Deduped.Count,
            DedupeMerges: unique.Merges,
            Scored: scored,
            Shortlist: shortlist,
            Dropped: dropped,
            ProbabilisticDedupe: unique.Probabilistic));

        yield return new CompleteEvent(runId, listingMatches);
    }
}
