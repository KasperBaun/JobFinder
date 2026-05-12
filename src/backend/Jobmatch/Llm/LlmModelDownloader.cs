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
// Single global per-process lock so two parallel download requests don't trample.
public sealed class LlmModelDownloader(HttpClient http, ILogger<LlmModelDownloader> logger)
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

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
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            var tmp = destPath + ".download";
            if (File.Exists(tmp)) File.Delete(tmp);

            logger.LogInformation("Downloading LLM model from {Url} → {Dest}", downloadUrl, destPath);

            using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long downloaded = 0;
            var lastReport = downloaded;

            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (read == 0) break;
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                // Report ~every 4 MB to avoid SSE spam on a 2.3 GB file.
                if (downloaded - lastReport >= 4_000_000)
                {
                    lastReport = downloaded;
                    yield return new DownloadProgress(downloaded, total);
                }
            }

            yield return new DownloadProgress(downloaded, total);
            await file.FlushAsync(ct);
            file.Close();
            stream.Close();
            File.Move(tmp, destPath, overwrite: true);
            logger.LogInformation("LLM model download complete ({Bytes} bytes)", downloaded);
        }
        finally
        {
            Lock.Release();
        }
    }
}
