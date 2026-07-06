using Jobmatch.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Infrastructure;

public enum ModelDownloadState { Idle, Downloading, Completed, Failed }

public sealed record ModelDownloadSnapshot(
    ModelDownloadState State,
    long DownloadedBytes,
    long? TotalBytes,
    string? Error);

// Process-singleton that runs the GGUF model download on a background task, decoupled from the HTTP
// request that starts it. The transfer therefore survives SPA navigation and a full page reload (the
// backend process stays up), unlike the old request-bound SSE stream that died with RequestAborted.
// Live progress is held in memory and read back through GET /api/llm/status; the SPA polls that while
// downloading, so a client that navigates away and returns reconnects to the real state instead of
// losing it. Same spirit as the search job outliving its request.
public sealed class ModelDownloadManager(
    IServiceScopeFactory scopeFactory,
    ILogger<ModelDownloadManager> logger)
{
    private readonly object _gate = new();
    private ModelDownloadState _state = ModelDownloadState.Idle;
    private long _downloaded;
    private long? _total;
    private string? _error;

    public ModelDownloadSnapshot Snapshot()
    {
        lock (_gate)
            return Current();
    }

    // Idempotent. If the file is already on disk → Completed with no work. If a download is already
    // running → no-op; the caller just observes progress via Snapshot(). Otherwise (Idle/Completed/
    // Failed) a fresh background transfer is launched — so a retry after a failure just works.
    public ModelDownloadSnapshot Start(string downloadUrl, string destPath)
    {
        lock (_gate)
        {
            if (_state == ModelDownloadState.Downloading)
                return Current();

            if (File.Exists(destPath))
            {
                _state = ModelDownloadState.Completed;
                _downloaded = new FileInfo(destPath).Length;
                _total = _downloaded;
                _error = null;
                return Current();
            }

            _state = ModelDownloadState.Downloading;
            _downloaded = 0;
            _total = null;
            _error = null;
            _ = Task.Run(() => RunAsync(downloadUrl, destPath));
            return Current();
        }
    }

    private ModelDownloadSnapshot Current() => new(_state, _downloaded, _total, _error);

    private async Task RunAsync(string downloadUrl, string destPath)
    {
        try
        {
            // Resolve the typed-HttpClient downloader from a fresh scope rather than capturing it in
            // this singleton (avoids a captive HttpClient). CancellationToken.None: the download is
            // deliberately independent of any request; it ends by completing, failing, or process exit.
            using var scope = scopeFactory.CreateScope();
            var downloader = scope.ServiceProvider.GetRequiredService<LlmModelDownloader>();
            await foreach (var p in downloader.DownloadAsync(downloadUrl, destPath, CancellationToken.None).ConfigureAwait(false))
            {
                lock (_gate)
                {
                    _downloaded = p.DownloadedBytes;
                    _total = p.TotalBytes;
                }
            }
            lock (_gate)
                _state = ModelDownloadState.Completed;
            logger.LogInformation("LLM model download complete");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException is { Message: var inner } ? $"{ex.Message} ({inner})" : ex.Message;
            lock (_gate)
            {
                _state = ModelDownloadState.Failed;
                _error = message;
            }
            logger.LogError(ex, "LLM model download failed");
        }
    }
}
