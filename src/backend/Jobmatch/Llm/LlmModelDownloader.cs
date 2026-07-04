using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Llm;

public sealed record DownloadProgress(long DownloadedBytes, long? TotalBytes);

public sealed record ModelStatus(
    bool Present,
    string Path,
    long? CurrentBytes,
    long? ExpectedBytes,
    string DownloadUrl);

// Streams the GGUF model file from the configured download URL to disk, writing in
// chunks so the caller can report progress to the user. Atomic via .download tmp
// file + rename — partial downloads from a crashed run get cleared on next start.
// The transfer is a multi-GB stream over networks (corporate proxies, VPNs) that
// intermittently reset a long-lived TLS connection: a single reset surfaces as
// "The SSL connection could not be established" / "connection forcibly closed".
// So a transient failure is retried and the download resumes from the partial file
// via an HTTP Range request instead of restarting from zero. Single global per-process
// lock so two parallel download requests don't trample.
public sealed class LlmModelDownloader(HttpClient http, ILogger<LlmModelDownloader> logger)
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    // Bail only after this many *consecutive* attempts that made no forward progress. A reset
    // that still advanced the download resets the counter, so a flaky link that keeps inching
    // forward will complete rather than being abandoned at an arbitrary offset.
    private const int MaxConsecutiveStalls = 5;

    public ModelStatus GetStatus(string modelPath, string downloadUrl)
    {
        var present = File.Exists(modelPath);
        long? current = present ? new FileInfo(modelPath).Length : null;
        return new ModelStatus(
            Present: present,
            Path: modelPath,
            CurrentBytes: current,
            ExpectedBytes: null, // populated by HEAD probe inside Download; cheap to omit here
            DownloadUrl: downloadUrl);
    }

    public async IAsyncEnumerable<DownloadProgress> DownloadAsync(
        string downloadUrl,
        string destPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // The retry/resume loop reports progress through a channel so its try/catch never
            // wraps a `yield` (illegal in C#). The producer completes the channel with any
            // terminal exception, which surfaces here as the enumeration throws.
            var channel = Channel.CreateUnbounded<DownloadProgress>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

            var producer = Task.Run(async () =>
            {
                try
                {
                    await DownloadWithResumeAsync(downloadUrl, destPath, channel.Writer, ct).ConfigureAwait(false);
                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                }
            }, ct);

            await foreach (var progress in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                yield return progress;

            await producer.ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    private async Task DownloadWithResumeAsync(
        string downloadUrl,
        string destPath,
        ChannelWriter<DownloadProgress> writer,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var tmp = destPath + ".download";
        if (File.Exists(tmp)) File.Delete(tmp);

        logger.LogInformation("Downloading LLM model from {Url} → {Dest}", downloadUrl, destPath);

        long? total = null;
        long downloaded = 0;
        var consecutiveStalls = 0;

        while (true)
        {
            var attemptStart = downloaded;
            try
            {
                await DownloadAttemptAsync(downloadUrl, tmp, downloaded, t => total ??= t, writer, ct)
                    .ConfigureAwait(false);
                downloaded = FileLength(tmp);
                break; // read to EOF — done
            }
            catch (Exception ex) when (IsTransient(ex) && !ct.IsCancellationRequested)
            {
                // The partial file (flushed on the failed stream's dispose) is the source of truth
                // for how far we actually got — the retry resumes from here via a Range request.
                downloaded = FileLength(tmp);
                if (downloaded > attemptStart)
                {
                    consecutiveStalls = 0;
                }
                else if (++consecutiveStalls >= MaxConsecutiveStalls)
                {
                    logger.LogError(ex,
                        "LLM model download failed after {Stalls} consecutive attempts with no progress at {Bytes} bytes",
                        consecutiveStalls, downloaded);
                    throw;
                }

                var delay = consecutiveStalls == 0
                    ? TimeSpan.Zero // made progress this round — resume immediately
                    : TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, consecutiveStalls - 1)));
                logger.LogWarning(ex,
                    "LLM model download interrupted at {Bytes} bytes; retrying in {Delay}s (stall {Stalls}/{Max})",
                    downloaded, delay.TotalSeconds, consecutiveStalls, MaxConsecutiveStalls);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        await writer.WriteAsync(new DownloadProgress(downloaded, total ?? downloaded), ct).ConfigureAwait(false);
        File.Move(tmp, destPath, overwrite: true);
        logger.LogInformation("LLM model download complete ({Bytes} bytes)", downloaded);
    }

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    // Runs one connection attempt, appending to the partial file from byte <paramref name="from"/>.
    // Throws on any network failure so the caller can retry from the partial file's length. When the
    // server ignores the Range request it restarts from zero (truncates the partial file).
    private async Task DownloadAttemptAsync(
        string url,
        string tmp,
        long from,
        Action<long?> captureTotal,
        ChannelWriter<DownloadProgress> writer,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (from > 0)
            request.Headers.Range = new RangeHeaderValue(from, null);

        using var response = await http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (from > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            from = 0; // server ignored Range — start over from the beginning

        response.EnsureSuccessStatusCode();
        var total = ExtractTotal(response, from);
        captureTotal(total);

        var append = from > 0;
        await using var netStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(
            tmp, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        var downloaded = from;
        var lastReport = downloaded;
        while (true)
        {
            var read = await netStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            downloaded += read;
            // Report ~every 4 MB to avoid SSE spam on a 2.3 GB file.
            if (downloaded - lastReport >= 4_000_000)
            {
                lastReport = downloaded;
                await writer.WriteAsync(new DownloadProgress(downloaded, total ?? downloaded), ct).ConfigureAwait(false);
            }
        }

        await file.FlushAsync(ct).ConfigureAwait(false);
    }

    private static long? ExtractTotal(HttpResponseMessage response, long offset)
    {
        if (response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.Length is long full)
            return full;
        if (response.Content.Headers.ContentLength is long length)
            return offset + length;
        return null;
    }

    private static bool IsTransient(Exception ex)
    {
        // An HTTP error that carries a status: retry only server-side / throttle codes.
        if (ex is HttpRequestException { StatusCode: { } status })
            return status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

        // No status → the connection never produced a response: TLS/handshake reset, socket
        // reset, or an IO failure mid-stream. These are the transient cases worth resuming.
        return ex is HttpRequestException or IOException or SocketException
            || ex is TaskCanceledException { InnerException: TimeoutException };
    }
}
