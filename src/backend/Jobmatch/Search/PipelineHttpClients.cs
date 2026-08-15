namespace Jobmatch.Search;

/// <summary>
/// The two HTTP clients a run needs, and why they cannot be one.
/// </summary>
/// <remarks>
/// <para><see cref="Fetch"/> talks to job boards. It carries a bounded per-request timeout so a
/// single hanging employer page fails fast rather than stalling near the framework default of 100s,
/// and caps connections per host: several sources enrich against the same site (three jobindex
/// feeds × 10 concurrent enrichment fetches each), and unbounded parallel connection attempts made
/// hosts throttle setup until queued requests timed out.</para>
/// <para><see cref="Judge"/> talks to the local model. It is deliberately untimed — an Ollama
/// generation legitimately exceeds 30s, and cutting one off would silently drop a verdict.</para>
/// </remarks>
public sealed class PipelineHttpClients : IDisposable
{
    public HttpClient Fetch { get; } = new(new SocketsHttpHandler { MaxConnectionsPerServer = 8 })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public HttpClient Judge { get; } = new();

    public void Dispose()
    {
        Fetch.Dispose();
        Judge.Dispose();
    }
}
