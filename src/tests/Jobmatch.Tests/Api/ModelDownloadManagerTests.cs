using System.Diagnostics;
using System.Net;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Api;

// The manager exists so a download's live state outlives the request that started it — a client that
// navigates away and back (a fresh status poll) must still see it running. These tests drive that
// directly: observe an in-flight download from a separate call, prove Start is idempotent, and check
// completion / failure / already-present all resolve.
public sealed class ModelDownloadManagerTests
{
    private const string Url = "https://example.test/model.gguf";

    [Fact]
    public async Task InFlightDownload_IsObservableAcrossCalls_AndStartIsIdempotent()
    {
        // 9 MB so the downloader's ~4 MB progress report fires while we hold the transfer open.
        var full = new byte[9_000_000];
        new Random(1).NextBytes(full);
        var gate = new TaskCompletionSource();
        var handler = new GatedHandler(full, gateAt: 5_000_000, gate.Task);
        var manager = NewManager(handler, out var dest);
        try
        {
            var started = manager.Start(Url, dest);
            Assert.Equal(ModelDownloadState.Downloading, started.State);

            // A *separate* observer (what a status poll after navigating back is) sees it running,
            // with progress — the state was never bound to the starting request.
            await WaitUntil(() =>
            {
                var s = manager.Snapshot();
                return s.State == ModelDownloadState.Downloading && s.DownloadedBytes > 0;
            });

            // A repeat Start (second button click / another poller) must not launch a second download.
            var again = manager.Start(Url, dest);
            Assert.Equal(ModelDownloadState.Downloading, again.State);
            Assert.Equal(1, handler.Calls);

            gate.SetResult();
            await WaitUntil(() => manager.Snapshot().State == ModelDownloadState.Completed);

            Assert.Equal(1, handler.Calls);
            Assert.True(File.Exists(dest));
            Assert.Equal(full.Length, new FileInfo(dest).Length);
        }
        finally
        {
            gate.TrySetResult();
            Cleanup(dest);
        }
    }

    [Fact]
    public async Task FailedDownload_SurfacesError_AndLeavesNoFinalFile()
    {
        var handler = new StatusHandler(HttpStatusCode.NotFound);
        var manager = NewManager(handler, out var dest);
        try
        {
            manager.Start(Url, dest);
            await WaitUntil(() => manager.Snapshot().State == ModelDownloadState.Failed);
            Assert.NotNull(manager.Snapshot().Error);
            Assert.False(File.Exists(dest));
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public void Start_IsNoOp_WhenModelAlreadyPresent()
    {
        // A 404 handler that would fail if the network were ever touched.
        var handler = new StatusHandler(HttpStatusCode.NotFound);
        var manager = NewManager(handler, out var dest);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, new byte[123]);

            var snap = manager.Start(Url, dest);

            Assert.Equal(ModelDownloadState.Completed, snap.State);
            Assert.Equal(123, snap.DownloadedBytes);
            Assert.Equal(0, handler.Calls);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    private static ModelDownloadManager NewManager(HttpMessageHandler handler, out string dest)
    {
        var services = new ServiceCollection();
        services.AddTransient(_ => new LlmModelDownloader(new HttpClient(handler), NullLogger<LlmModelDownloader>.Instance));
        var provider = services.BuildServiceProvider();
        dest = Path.Combine(Path.GetTempPath(), "jm-mgr-tests", Guid.NewGuid().ToString("N"), "model.gguf");
        return new ModelDownloadManager(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ModelDownloadManager>.Instance);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new TimeoutException("condition was not met in time");
            await Task.Delay(20);
        }
    }

    private static void Cleanup(string dest)
    {
        var dir = Path.GetDirectoryName(dest)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    // Serves the payload but blocks once `gateAt` bytes have been read, until `gate` completes —
    // holding the download mid-flight so the test can observe it from another call.
    private sealed class GatedHandler(byte[] full, int gateAt, Task gate) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var content = new StreamContent(new GatedStream(full, gateAt, gate));
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

    private sealed class GatedStream(byte[] data, int gateAt, Task gate) : Stream
    {
        private int _pos;
        private bool _gated;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_gated && _pos >= gateAt)
            {
                _gated = true;
                await gate.ConfigureAwait(false);
            }
            if (_pos >= data.Length) return 0;
            var n = Math.Min(buffer.Length, data.Length - _pos);
            data.AsSpan(_pos, n).CopyTo(buffer.Span);
            _pos += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
