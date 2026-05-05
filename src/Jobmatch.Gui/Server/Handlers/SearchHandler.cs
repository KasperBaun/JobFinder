using Jobmatch.Search;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class SearchHandler
{
    public static async Task Run(SearchRequest req, Jobmatch.UserContext ctx, HttpContext http, CancellationToken ct)
    {
        SseHelper.SetHeaders(http);

        try
        {
            var service = new SearchService(ctx);
            await foreach (var evt in service.RunAsync(req ?? new SearchRequest(), ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                await SseHelper.SendAsync(http, (SearchProgressEvent)evt).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing more to do.
        }
        catch (Exception ex)
        {
            try
            {
                await SseHelper.SendAsync(http, (SearchProgressEvent)new ErrorEvent(ex.Message)).ConfigureAwait(false);
            }
            catch
            {
                // The connection may already be torn down — swallow secondary failures.
            }
        }
    }
}
