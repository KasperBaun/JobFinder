using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

/// <summary>
/// Stands in for link discovery so service tests never reach the network. Returns whatever it was
/// seeded with, regardless of URL; the default is "found nothing", which is what every test that
/// does not care about discovery wants.
/// </summary>
public sealed class FakeSourceDiscovery(params SourceCandidate[] candidates) : ISourceDiscoveryService
{
    public Uri? LastUrl { get; private set; }

    public Task<IReadOnlyList<SourceCandidate>> DiscoverAsync(Uri pageUrl, CancellationToken ct)
    {
        LastUrl = pageUrl;
        return Task.FromResult<IReadOnlyList<SourceCandidate>>(candidates);
    }
}
