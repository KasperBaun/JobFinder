namespace Jobmatch.Api.Models;

// Polled by the GUI to decide whether to show the model-download banner.
public sealed record LlmStatusResponse(
    bool Enabled,
    string Provider,
    bool ModelPresent,
    string ModelPath,
    long? ModelSizeBytes,
    string DownloadUrl);

public sealed record LlmDownloadProgressEvent(
    long DownloadedBytes,
    long? TotalBytes);

public sealed record LlmDownloadCompleteEvent(string ModelPath, long Bytes);
