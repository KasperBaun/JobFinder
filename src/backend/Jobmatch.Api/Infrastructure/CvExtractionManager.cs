using Jobmatch.Models;
using Jobmatch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Infrastructure;

public enum CvExtractionState { Idle, Extracting, Completed, Failed }

public sealed record CvExtractionSnapshot(
    CvExtractionState State,
    DateTimeOffset? StartedAt,
    string? Error,
    ExtractedProfile? Profile);

// Process-singleton that runs the CV → profile extraction on a background task, decoupled from
// the HTTP request that starts it (same pattern as ModelDownloadManager): CPU inference takes
// 30-90s, so the SPA polls GET /api/skillset/extract/status and survives navigation/reload.
// The result is prefill-only — nothing is persisted here — so in-memory state is enough.
public sealed class CvExtractionManager(
    IServiceScopeFactory scopeFactory,
    ILogger<CvExtractionManager> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private CvExtractionState _state = CvExtractionState.Idle;
    private DateTimeOffset? _startedAt;
    private string? _error;
    private ExtractedProfile? _profile;

    public CvExtractionSnapshot Snapshot()
    {
        lock (_gate)
            return Current();
    }

    // Idempotent while running: a repeat call observes the in-flight run instead of starting a
    // second one. From Idle/Completed/Failed a fresh extraction starts, so retry just works.
    public CvExtractionSnapshot Start(CvSource source)
    {
        lock (_gate)
        {
            if (_state == CvExtractionState.Extracting)
                return Current();

            _state = CvExtractionState.Extracting;
            _startedAt = DateTimeOffset.UtcNow;
            _error = null;
            _profile = null;
            _ = Task.Run(() => RunAsync(source));
            return Current();
        }
    }

    private CvExtractionSnapshot Current() => new(_state, _startedAt, _error, _profile);

    private async Task RunAsync(CvSource source)
    {
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICvExtractionService>();
            var profile = await service.ExtractAsync(source, cts.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _state = CvExtractionState.Completed;
                _profile = profile;
            }
            logger.LogInformation("CV extraction complete");
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _state = CvExtractionState.Failed;
                _error = $"Extraction timed out after {Timeout.TotalMinutes:0} minutes.";
            }
            logger.LogWarning("CV extraction timed out");
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = CvExtractionState.Failed;
                _error = ex.Message;
            }
            logger.LogError(ex, "CV extraction failed");
        }
    }
}
