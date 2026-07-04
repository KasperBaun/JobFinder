using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Jobmatch.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Llm;

public sealed class LlmModelDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_ResumesFromPartial_AfterMidStreamReset()
    {
        var full = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        // First response streams 8 bytes then throws (connection reset); the retry must
        // resume from byte 8 via a Range request rather than restarting from zero.
        var handler = new ResumeScriptHandler(full, failFirstAfter: 8);
        using var http = new HttpClient(handler);
        var downloader = new LlmModelDownloader(http, NullLogger<LlmModelDownloader>.Instance);

        var dest = Path.Combine(Path.GetTempPath(), "jm-dl-tests", Guid.NewGuid().ToString("N"), "model.gguf");
        try
        {
            var progress = new List<DownloadProgress>();
            await foreach (var p in downloader.DownloadAsync("https://example.test/model.gguf", dest))
                progress.Add(p);

            Assert.True(File.Exists(dest));
            Assert.Equal(full, await File.ReadAllBytesAsync(dest));
            Assert.Equal(2, handler.Calls);
            Assert.Null(handler.RangeStarts[0]);       // first attempt: no Range (full GET)
            Assert.Equal(8, handler.RangeStarts[1]);   // retry resumed from byte 8
            Assert.NotEmpty(progress);
            Assert.Equal(20, progress[^1].DownloadedBytes);
        }
        finally
        {
            var dir = Path.GetDirectoryName(dest)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_RetriesConnectFailure_ThenSucceeds()
    {
        var full = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
        // First attempt fails at connect (handshake reset — the user's exact symptom);
        // the second attempt connects and downloads the whole file.
        var handler = new ConnectFailThenServeHandler(full, failFirstConnects: 1);
        using var http = new HttpClient(handler);
        var downloader = new LlmModelDownloader(http, NullLogger<LlmModelDownloader>.Instance);

        var dest = Path.Combine(Path.GetTempPath(), "jm-dl-tests", Guid.NewGuid().ToString("N"), "model.gguf");
        try
        {
            await foreach (var _ in downloader.DownloadAsync("https://example.test/model.gguf", dest)) { }
            Assert.Equal(full, await File.ReadAllBytesAsync(dest));
            Assert.Equal(2, handler.Calls);
        }
        finally
        {
            var dir = Path.GetDirectoryName(dest)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_DoesNotRetry_On404()
    {
        var handler = new StatusHandler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        var downloader = new LlmModelDownloader(http, NullLogger<LlmModelDownloader>.Instance);
        var dest = Path.Combine(Path.GetTempPath(), "jm-dl-tests", Guid.NewGuid().ToString("N"), "model.gguf");
        try
        {
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (var _ in downloader.DownloadAsync("https://example.test/model.gguf", dest)) { }
            });
            Assert.Equal(1, handler.Calls); // a 4xx is terminal — no retry
        }
        finally
        {
            var dir = Path.GetDirectoryName(dest)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class ResumeScriptHandler(byte[] full, int failFirstAfter) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<long?> RangeStarts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var from = request.Headers.Range?.Ranges.FirstOrDefault()?.From;
            RangeStarts.Add(from);
            var start = (int)(from ?? 0);
            var segment = full[start..];

            if (Calls == 1)
            {
                var content = new StreamContent(new FaultyStream(segment, failFirstAfter));
                content.Headers.ContentLength = full.Length;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            var resume = new ByteArrayContent(segment);
            resume.Headers.ContentLength = segment.Length;
            resume.Headers.ContentRange = new ContentRangeHeaderValue(start, full.Length - 1, full.Length);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = resume });
        }
    }

    private sealed class ConnectFailThenServeHandler(byte[] full, int failFirstConnects) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failFirstConnects)
                throw new HttpRequestException("The SSL connection could not be established.");

            var content = new ByteArrayContent(full);
            content.Headers.ContentLength = full.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent([]) });
        }
    }

    // Emits up to failAfter bytes across reads, then throws to simulate a connection reset.
    private sealed class FaultyStream(byte[] data, int failAfter) : Stream
    {
        private int _pos;

        public override int Read(byte[] buffer, int offset, int count)
            => ReadCore(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ReadCore(buffer.Span));

        private int ReadCore(Span<byte> buffer)
        {
            if (_pos >= failAfter)
                throw new IOException("Unable to read data from the transport connection: connection reset.");
            var available = Math.Min(failAfter, data.Length) - _pos;
            var n = Math.Min(buffer.Length, available);
            data.AsSpan(_pos, n).CopyTo(buffer);
            _pos += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
