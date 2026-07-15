using System.Collections.Concurrent;

namespace Jobmatch.Adapters;

// Run-scoped state shared by every adapter's body enrichment. Overlapping feeds are the norm
// (the six jobindex queries surface many of the same postings — ~30% duplicate URLs per run),
// so within one run each listing page is fetched at most once and shared, and a host that stops
// responding is discovered once run-wide instead of once per source. One instance per search
// run — never reuse across runs.
public sealed class BodyFetchSession
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _pages = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _hostFailures = new(StringComparer.OrdinalIgnoreCase);

    // De-duplicates fetches by absolute URL, including in-flight ones — a second requester awaits
    // the first's task instead of fetching again. Failed fetches are evicted so a transient error
    // seen by one source doesn't poison the URL for the rest of the run (the host breaker, not
    // the cache, is what stops retry storms against a dead host).
    internal async Task<string?> GetOrFetchAsync(Uri url, Func<Uri, Task<string?>> fetch)
    {
        var key = url.AbsoluteUri;
        var lazy = _pages.GetOrAdd(key, _ => new Lazy<Task<string?>>(() => fetch(url)));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            _pages.TryRemove(new KeyValuePair<string, Lazy<Task<string?>>>(key, lazy));
            throw;
        }
    }

    internal bool IsHostTripped(string host, int threshold) =>
        _hostFailures.TryGetValue(host, out var count) && count >= threshold;

    internal void RecordSuccess(string host) => _hostFailures[host] = 0;

    internal void RecordFailure(string host) => _hostFailures.AddOrUpdate(host, 1, (_, n) => n + 1);
}
