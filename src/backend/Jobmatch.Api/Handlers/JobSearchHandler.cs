using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Jobs;
using Jobmatch.Api.Models;
using Jobmatch.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IJobSearchHandler
{
    Task<IResult> Start(SearchRequest? request);
    Task<IResult> Get(string id);
    Task<IResult> Active();
    Task<IResult> Cancel(string id);
    Task Stream(string id, HttpContext http, CancellationToken ct);
}

public sealed class JobSearchHandler(
    IJobSearchService service,
    JobSearchBus bus,
    ILogger<JobSearchHandler> logger)
    : HandlerBase(logger), IJobSearchHandler
{
    public Task<IResult> Start(SearchRequest? request) => ExecuteAsync(
        "start search",
        () =>
        {
            var job = service.Create(request ?? new SearchRequest());
            return Task.FromResult<IResult>(Results.Ok(new StartSearchResponse(job.Id)));
        });

    public Task<IResult> Get(string id) => ExecuteAsync(
        "get search: {Id}",
        () =>
        {
            var job = service.Get(id);
            return Task.FromResult<IResult>(job is null ? Results.NotFound() : Results.Ok(job));
        },
        logParams: [id]);

    public Task<IResult> Active() => ExecuteAsync(
        "get active search",
        () =>
        {
            var job = service.Active();
            return Task.FromResult<IResult>(Results.Ok(job));
        });

    public Task<IResult> Cancel(string id) => ExecuteAsync(
        "cancel search: {Id}",
        () =>
        {
            service.Cancel(id);
            return Task.FromResult<IResult>(Results.Ok());
        },
        logParams: [id]);

    /// <summary>
    /// SSE stream of JobSearch snapshots. Registers with the bus first (so no update is missed), sends the
    /// current snapshot for replay-on-connect, then streams live snapshots until the run is terminal or the
    /// client disconnects. A client disconnect ends only this stream — it never cancels the background run.
    /// </summary>
    public async Task Stream(string id, HttpContext http, CancellationToken ct)
    {
        var current = service.Get(id);
        if (current is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        SseHelper.SetHeaders(http);

        // Subscribe before re-reading state so a terminal published in between isn't lost.
        using var sub = bus.Subscribe(id);

        var snapshot = service.Get(id) ?? current;
        await SseHelper.SendAsync(http, snapshot).ConfigureAwait(false);
        if (snapshot.IsTerminal) return;

        try
        {
            await foreach (var update in sub.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await SseHelper.SendAsync(http, update).ConfigureAwait(false);
                if (update.IsTerminal) return;
            }
        }
        catch (OperationCanceledException)
        {
            // Client navigated away / closed the tab. The background run keeps going.
        }
    }
}
