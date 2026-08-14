namespace Jobmatch.Features.Providers;

// Projects each provider's most-recent-run info out of the recorded runs. Reads through
// IRunHistoryStore and the typed RunDetail rather than walking the JSON by hand, so renaming a
// member of the run record cannot break the providers page alone.
public sealed partial class ProvidersService
{
    private Dictionary<string, LastFetch> LoadLastFetchByProvider()
    {
        var result = new Dictionary<string, LastFetch>(StringComparer.OrdinalIgnoreCase);

        // Newest first, so the first entry seen for a provider is its most recent run.
        foreach (var run in history.All())
        {
            foreach (var provider in run.Providers)
            {
                if (string.IsNullOrWhiteSpace(provider.Name)) continue;
                if (result.ContainsKey(provider.Name)) continue;
                result[provider.Name] = new LastFetch(run.StartedAt, provider.FetchedCount);
            }
        }

        return result;
    }

    private IReadOnlyList<ProviderRunHistory> LoadRecentRuns(string providerName, int take)
    {
        var result = new List<ProviderRunHistory>();

        foreach (var run in history.All())
        {
            if (result.Count >= take) break;

            var provider = run.Providers.FirstOrDefault(
                p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
            if (provider is null) continue;

            result.Add(new ProviderRunHistory(
                RunId: run.RunId,
                StartedAt: run.StartedAt,
                Status: provider.Status.ToString().ToLowerInvariant(),
                FetchedCount: provider.FetchedCount,
                Error: provider.Error));
        }

        return result;
    }
}
