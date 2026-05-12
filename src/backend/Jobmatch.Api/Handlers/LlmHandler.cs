using Jobmatch;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Configuration;
using Jobmatch.Llm;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ILlmHandler
{
    Task<IResult> Status();
    Task DownloadModel(HttpContext http, CancellationToken ct);
}

public sealed class LlmHandler(
    UserContext ctx,
    LlmModelDownloader downloader,
    ILogger<LlmHandler> logger) : HandlerBase(logger), ILlmHandler
{
    public Task<IResult> Status() => ExecuteAsync(
        "llm status",
        () =>
        {
            var ranking = RankingConfigLoader.Load(ctx.RankingPath);
            var llm = ranking.Llm;
            var resolvedPath = ResolveModelPath(llm.ModelPath, ctx.RootDir);
            var status = downloader.GetStatus(resolvedPath, llm.ModelDownloadUrl);
            var response = new LlmStatusResponse(
                Enabled: llm.Enabled,
                Provider: llm.Provider,
                ModelPresent: status.Present,
                ModelPath: status.Path,
                ModelSizeBytes: status.CurrentBytes,
                DownloadUrl: status.DownloadUrl);
            return Task.FromResult<IResult>(Results.Ok(response));
        });

    public async Task DownloadModel(HttpContext http, CancellationToken ct)
    {
        SseHelper.SetHeaders(http);
        var ranking = RankingConfigLoader.Load(ctx.RankingPath);
        var llm = ranking.Llm;
        var path = ResolveModelPath(llm.ModelPath, ctx.RootDir);

        Logger.LogInformation("Starting LLM model download: {Url} → {Path}", llm.ModelDownloadUrl, path);
        long lastBytes = 0;
        try
        {
            await foreach (var p in downloader.DownloadAsync(llm.ModelDownloadUrl, path, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                lastBytes = p.DownloadedBytes;
                await SseHelper.SendAsync(http, new { type = "progress", downloadedBytes = p.DownloadedBytes, totalBytes = p.TotalBytes }).ConfigureAwait(false);
            }
            await SseHelper.SendAsync(http, new { type = "complete", modelPath = path, bytes = lastBytes }).ConfigureAwait(false);
            Logger.LogInformation("LLM model download finished ({Bytes} bytes)", lastBytes);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("LLM model download cancelled by client");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LLM model download failed");
            try { await SseHelper.SendAsync(http, new { type = "error", message = ex.Message }).ConfigureAwait(false); }
            catch { /* connection may be torn down */ }
        }
    }

    private static string ResolveModelPath(string configured, string userDataDir)
        => Path.IsPathRooted(configured) ? configured : Path.Combine(userDataDir, configured);
}
