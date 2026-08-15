using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Jobmatch.Domain;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Fetching.Adapters;
using Jobmatch.Infrastructure.IO;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Search.Fetching;

/// <summary>Everything a source returned, in the order the sources were listed.</summary>
public sealed record FetchOutcome(
    IReadOnlyList<Listing> Listings,
    IReadOnlyList<ProviderRunStatus> Statuses,
    IReadOnlyDictionary<string, IReadOnlyList<Listing>> ByProvider);

/// <summary>
/// Fetches every enabled source concurrently, reporting each one's progress as it happens, and
/// reassembles the results deterministically.
/// </summary>
/// <remarks>
/// A source that fails or hangs is recorded as failed and the run continues on the ones that
/// returned — a single unreachable board must never cost the user their whole search.
/// </remarks>
public sealed class ProviderFetch(
    string importsDirectory,
    IFileSystem fs,
    ILoggerFactory loggers,
    TimeSpan perSourceTimeout)
{
    /// <summary>One provider's fetch outcome, held in enabled-order so result assembly stays deterministic.</summary>
    private readonly record struct Slot(
        string Name, IReadOnlyList<Listing> Results, string? Error, long DurationMs, bool HitPageCap, bool PossiblyCapped);

    /// <summary>
    /// Yields a progress event per source as it starts and finishes; the completed
    /// <see cref="FetchOutcome"/> is handed to <paramref name="onCompleted"/> once every source has
    /// settled. Results are assembled in enabled-order rather than completion-order, so first-wins
    /// dedupe downstream is deterministic regardless of which source returns first.
    /// </summary>
    public async IAsyncEnumerable<SearchProgressEvent> FetchAll(
        IReadOnlyList<PortalConfig> enabled,
        HttpClient http,
        Action<FetchOutcome> onCompleted,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var total = enabled.Count;
        if (total == 0)
        {
            onCompleted(new FetchOutcome([], [], new Dictionary<string, IReadOnlyList<Listing>>(StringComparer.Ordinal)));
            yield break;
        }

        var slots = new Slot[total];
        var channel = Channel.CreateUnbounded<SearchProgressEvent>();
        // One enrichment session for the whole run: overlapping feeds (six jobindex queries)
        // share fetched pages and the has-this-host-gone-dark verdict instead of each paying
        // for the same page or the same discovery.
        var bodySession = new BodyFetchSession();

        async Task FetchOne(int i)
        {
            var portal = enabled[i];
            var index = i + 1;
            await channel.Writer.WriteAsync(new ProviderRunningEvent(portal.Name, index, total), ct).ConfigureAwait(false);
            var sw = Stopwatch.StartNew();
            var (results, error, hitPageCap) = await FetchSafe(portal, http, bodySession, ct).ConfigureAwait(false);
            var durationMs = sw.ElapsedMilliseconds;
            var possiblyCapped = error is null && ProviderCapHeuristic.LimitReached(portal, results.Count);
            slots[i] = new Slot(portal.Name, results, error, durationMs, hitPageCap, possiblyCapped);
            await channel.Writer.WriteAsync(
                error is null
                    ? new ProviderDoneEvent(portal.Name, results.Count, index, total, durationMs, hitPageCap, possiblyCapped)
                    : new ProviderFailedEvent(portal.Name, error, index, total, durationMs),
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
        onCompleted(Collect(slots));
    }

    private static FetchOutcome Collect(Slot[] slots)
    {
        var listings = new List<Listing>();
        var statuses = new List<ProviderRunStatus>(slots.Length);
        var byProvider = new Dictionary<string, IReadOnlyList<Listing>>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            if (slot.Error is null)
            {
                listings.AddRange(slot.Results);
                byProvider[slot.Name] = slot.Results;
                statuses.Add(new ProviderRunStatus(
                    slot.Name, ProviderRunState.Ok, slot.Results.Count, null,
                    slot.DurationMs, slot.HitPageCap, slot.PossiblyCapped));
            }
            else
            {
                byProvider[slot.Name] = [];
                statuses.Add(new ProviderRunStatus(
                    slot.Name, ProviderRunState.Failed, null, slot.Error, slot.DurationMs));
            }
        }

        return new FetchOutcome(listings, statuses, byProvider);
    }

    private async Task<(IReadOnlyList<Listing> Results, string? Error, bool HitPageCap)> FetchSafe(
        PortalConfig portal,
        HttpClient http,
        BodyFetchSession bodySession,
        CancellationToken ct)
    {
        using var srcCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        srcCts.CancelAfter(perSourceTimeout);
        try
        {
            var logger = loggers.CreateLogger($"Adapter.{portal.Name}");
            var adapter = AdapterFactory.Create(portal, http, logger, importsDirectory, fs, bodySession);
            if (adapter is null)
            {
                return (Array.Empty<Listing>(), $"unsupported portal type '{portal.Type}'", false);
            }

            var results = await adapter.FetchAsync(srcCts.Token).ConfigureAwait(false);
            return (results.Select(ListingTextDecoder.Decode).ToList(), null, adapter.HitPageCap);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return (Array.Empty<Listing>(), $"timed out after {perSourceTimeout.TotalSeconds:0}s", false);
        }
        catch (Exception ex)
        {
            return (Array.Empty<Listing>(), ex.Message, false);
        }
    }
}
