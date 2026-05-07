using Jobmatch.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISearchHandler
{
    Task Run(SearchRequest? request, HttpContext http, CancellationToken ct);
}

public sealed class SearchHandler(ISearchService search, ILogger<SearchHandler> logger) : ISearchHandler
{
    public async Task Run(SearchRequest? request, HttpContext http, CancellationToken ct)
    {
        // SSE streams progress events; we cannot return IResult after headers go out.
        // HandlerBase.ExecuteAsync doesn't fit — errors must be sent down the stream as ErrorEvents,
        // not translated to HTTP status codes.
        SseHelper.SetHeaders(http);

        logger.LogInformation("Starting search run");

        try
        {
            await foreach (var evt in search.RunAsync(request ?? new SearchRequest(), ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                await SseHelper.SendAsync(http, (SearchProgressEvent)evt).ConfigureAwait(false);
            }

            logger.LogInformation("Completed search run");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Search run cancelled by client");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search run failed");
            try
            {
                await SseHelper.SendAsync(http, (SearchProgressEvent)new ErrorEvent(ex.Message)).ConfigureAwait(false);
            }
            catch
            {
                // Connection may already be torn down — swallow secondary failures.
            }
        }
    }
}
