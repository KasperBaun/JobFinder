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
    Task<IResult> StartDownload();
}

public sealed class LlmHandler(
    UserContext ctx,
    LlmModelDownloader downloader,
    ModelDownloadManager downloads,
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
            var dl = downloads.Snapshot();
            var response = new LlmStatusResponse(
                Enabled: llm.Enabled,
                Provider: llm.Provider,
                ModelPresent: status.Present,
                ModelPath: status.Path,
                ModelSizeBytes: status.CurrentBytes,
                DownloadUrl: status.DownloadUrl,
                Download: new LlmDownloadStatus(dl.State, dl.DownloadedBytes, dl.TotalBytes, dl.Error));
            return Task.FromResult<IResult>(Results.Ok(response));
        });

    // Starts the download in the background and returns immediately. Idempotent: a repeat call while a
    // download is already running is a no-op. Progress is observed by polling Status(), so the transfer
    // is not tied to this request and survives the client navigating away or reloading.
    public Task<IResult> StartDownload() => ExecuteAsync(
        "start llm model download",
        () =>
        {
            var ranking = RankingConfigLoader.Load(ctx.RankingPath);
            var llm = ranking.Llm;
            var path = ResolveModelPath(llm.ModelPath, ctx.RootDir);
            var snapshot = downloads.Start(llm.ModelDownloadUrl, path);
            Logger.LogInformation("LLM model download requested → state {State}", snapshot.State);
            var body = new LlmDownloadStatus(snapshot.State, snapshot.DownloadedBytes, snapshot.TotalBytes, snapshot.Error);
            return Task.FromResult<IResult>(Results.Ok(body));
        });

    private static string ResolveModelPath(string configured, string userDataDir)
        => Path.IsPathRooted(configured) ? configured : Path.Combine(userDataDir, configured);
}
