namespace Jobmatch.Search;

public interface ISearchService
{
    IAsyncEnumerable<SearchProgressEvent> RunAsync(SearchRequest req, CancellationToken ct = default);

    /// <summary>Run with a caller-supplied run id (used by the background job so the id is known before execution).</summary>
    IAsyncEnumerable<SearchProgressEvent> RunAsync(SearchRequest req, string runId, CancellationToken ct = default);
}
