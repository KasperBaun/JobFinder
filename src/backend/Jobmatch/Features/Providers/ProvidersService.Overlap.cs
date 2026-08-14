using Microsoft.Extensions.Logging;

namespace Jobmatch.Features.Providers;

// "Do you already have this?" — answered by comparing the jobs a candidate actually returned against
// the jobs the sources you already have return, rather than by matching endpoint strings. Endpoint
// matching misses the cases that matter: the same board reached through a careers page, a company
// feed that duplicates an aggregator, a catalog entry whose URL has since moved.
public sealed partial class ProvidersService
{
    // Every probe is a live fetch of somebody else's board, so the candidate list is ranked cheaply
    // first and only the most plausible few are actually run.
    private const int MaxOverlapProbes = 3;
    private const double MinProbeScore = 30;

    private async Task<SourceOverlapMatch?> FindOverlapAsync(
        PortalConfig draft,
        IReadOnlyList<ProviderTestSample> samples,
        CancellationToken ct)
    {
        if (samples.Count < SourceOverlap.MinComparableCount) return null;

        var newUrls = samples.Select(s => s.Url).ToList();
        var probes = RankProbes(catalog.Effective(), draft, DominantHost(newUrls));

        SourceOverlapMatch? best = null;
        foreach (var portal in probes)
        {
            ct.ThrowIfCancellationRequested();

            ProviderTestOutcome outcome;
            try
            {
                outcome = await TestConfigAsync(portal, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Overlap probe failed for {ProviderName}", portal.Name);
                continue;
            }
            if (!outcome.Ok) continue;

            var match = SourceOverlap.Compare(
                portal.Id,
                DisplayNameOf(portal),
                newUrls,
                [.. outcome.Samples.Select(s => s.Url)]);

            if (match is not null && (best is null || match.Ratio > best.Ratio)) best = match;
            if (best is { Duplicate: true }) break;
        }

        return best;
    }

    /// <summary>
    /// The fuzzy pre-filter. Three independent signals, because any one of them alone is wrong often
    /// enough: the tenant host the draft calls, the host its jobs actually live on (an ATS board and
    /// its careers-page front door share the latter but not the former), and how close the names read.
    /// </summary>
    internal static IEnumerable<PortalConfig> RankProbes(
        IReadOnlyList<PortalConfig> catalog,
        PortalConfig draft,
        string? jobHost)
    {
        var draftHost = draft.Endpoint?.Host.ToLowerInvariant();
        var draftName = draft.DisplayName ?? draft.Name;

        // No self-exclusion by name: the draft is not in the catalog yet, so a catalog entry that
        // already carries the name we derived is the strongest hint there is, not a self-reference.
        return catalog
            .Where(p => p.Type != PortalType.Manual && p.Endpoint is not null)
            .Select(p => (Portal: p, Score: ProbeScore(p, draftHost, jobHost, draftName)))
            .Where(x => x.Score >= MinProbeScore)
            .OrderByDescending(x => x.Score)
            .Take(MaxOverlapProbes)
            .Select(x => x.Portal);
    }

    private static double ProbeScore(PortalConfig portal, string? draftHost, string? jobHost, string draftName)
    {
        var host = portal.Endpoint!.Host.ToLowerInvariant();
        double score = 0;
        if (draftHost is not null && host == draftHost) score += 100;
        if (jobHost is not null && host == jobHost) score += 80;

        var company = portal.StaticFields is not null && portal.StaticFields.TryGetValue("company", out var c) ? c : null;
        var nameScore = Math.Max(
            SourceOverlap.NameSimilarity(DisplayNameOf(portal), draftName),
            SourceOverlap.NameSimilarity(company, draftName));
        return score + (nameScore * 60);
    }

    // Where the jobs themselves live, which is often a different host from the API that lists them.
    internal static string? DominantHost(IReadOnlyList<string> urls)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
            var host = uri.Host.ToLowerInvariant();
            counts[host] = counts.GetValueOrDefault(host) + 1;
        }
        if (counts.Count == 0) return null;

        var top = counts.OrderByDescending(kvp => kvp.Value).First();
        return top.Value * 2 >= urls.Count ? top.Key : null;
    }

    private static string DisplayNameOf(PortalConfig p) =>
        string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName!;
}
