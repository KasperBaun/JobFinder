using Jobmatch.Api.Infrastructure;

namespace Jobmatch.Api.Models;

// Polled by the GUI to decide whether to show the model-download banner and to render live
// download progress (Download) — the poll is how a client reconnects to an in-flight download
// after navigating away or reloading.
public sealed record LlmStatusResponse(
    bool Enabled,
    string Provider,
    bool ModelPresent,
    string ModelPath,
    long? ModelSizeBytes,
    string DownloadUrl,
    LlmDownloadStatus Download);

public sealed record LlmDownloadStatus(
    ModelDownloadState State,
    long DownloadedBytes,
    long? TotalBytes,
    string? Error);
