namespace Jobmatch.Features.Providers;

public interface ISourceDiscoveryService
{
    /// <summary>
    /// Last resort when a pasted URL matches no known pattern: fetch the page and look for a link to
    /// a board this app does recognise. Returns at most one candidate per platform, best first.
    /// </summary>
    Task<IReadOnlyList<SourceCandidate>> DiscoverAsync(Uri pageUrl, CancellationToken ct);
}
