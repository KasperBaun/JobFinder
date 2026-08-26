using Jobmatch.Features.Drafting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Features.Drafting;

public enum DraftState { Idle, Drafting, Completed, Failed }

/// <summary>
/// Process-singleton that runs drafting on a background task, decoupled from the HTTP request that
/// starts it (same pattern as <c>CvExtractionManager</c>): two documents on CPU inference take
/// minutes, so the SPA polls <c>GET /api/drafts/status</c> and survives navigation or reload.
/// The documents are written to disk by the service, so in-memory state here is only the progress
/// of the run in flight.
/// </summary>
public sealed class DraftManager(
    IServiceScopeFactory scopeFactory,
    ILogger<DraftManager> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(20);

    private readonly object _gate = new();
    private DraftState _state = DraftState.Idle;
    private string? _runId;
    private string? _listingId;
    private DateTimeOffset? _startedAt;
    private string? _error;
    private DraftedDocuments? _result;

    public DraftStatusResponse Snapshot()
    {
        lock (_gate)
            return Current();
    }

    /// <summary>
    /// Idempotent while running: a repeat call observes the in-flight draft instead of starting a
    /// second one, so a double-click cannot put two model loads in memory at once.
    /// </summary>
    public DraftStatusResponse Start(DraftRequest request)
    {
        lock (_gate)
        {
            if (_state == DraftState.Drafting)
                return Current();

            _state = DraftState.Drafting;
            _runId = request.RunId;
            _listingId = request.ListingId;
            _startedAt = DateTimeOffset.UtcNow;
            _error = null;
            _result = null;
            _ = Task.Run(() => RunAsync(request));
            return Current();
        }
    }

    private DraftStatusResponse Current() =>
        new(_state, _runId, _listingId, _startedAt, _error, _result);

    private async Task RunAsync(DraftRequest request)
    {
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IApplicationDraftService>();
            var result = await service.DraftAsync(request.RunId, request.ListingId, cts.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _state = DraftState.Completed;
                _result = result;
            }
            logger.LogInformation("Drafted application for listing {ListingId}", request.ListingId);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _state = DraftState.Failed;
                _error = $"Drafting timed out after {Timeout.TotalMinutes:0} minutes.";
            }
            logger.LogWarning("Drafting timed out for listing {ListingId}", request.ListingId);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = DraftState.Failed;
                _error = ex.Message;
            }
            logger.LogError(ex, "Drafting failed for listing {ListingId}", request.ListingId);
        }
    }
}
