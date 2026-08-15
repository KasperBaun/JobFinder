using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.AiModel;
using Jobmatch;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Features.Llm;

public interface ILlmHandler
{
    Task<IResult> Status();
    Task<IResult> StartDownload();
}

public sealed class LlmHandler(
    ILlmModelLocator model,
    LlmModelDownloader downloader,
    ModelDownloadManager downloads,
    ILogger<LlmHandler> logger) : HandlerBase(logger), ILlmHandler
{
    public Task<IResult> Status() => ExecuteAsync(
        "llm status",
        () =>
        {
            var llm = model.Config;
            var required = model.RequiredModel;
            var dl = downloads.Snapshot();

            // A provider that serves its own model has nothing local to fetch, so it is already
            // "present" — the GUI gates its download banner and its AI-ready badge on this flag.
            var status = required is null
                ? new ModelStatus(Present: true, Path: "", CurrentBytes: null, ExpectedBytes: null, DownloadUrl: "")
                : downloader.GetStatus(required.AbsolutePath, required.DownloadUrl.ToString());

            var response = new LlmStatusResponse(
                Enabled: llm.Enabled,
                Provider: llm.Provider.Name,
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
            if (model.RequiredModel is not { } required)
                throw new InvalidRequestException(
                    $"The {model.Config.Provider.Name} provider serves its own model — there is nothing to download.");

            var snapshot = downloads.Start(required.DownloadUrl.ToString(), required.AbsolutePath);
            Logger.LogInformation("LLM model download requested → state {State}", snapshot.State);
            var body = new LlmDownloadStatus(snapshot.State, snapshot.DownloadedBytes, snapshot.TotalBytes, snapshot.Error);
            return Task.FromResult<IResult>(Results.Ok(body));
        });
}
