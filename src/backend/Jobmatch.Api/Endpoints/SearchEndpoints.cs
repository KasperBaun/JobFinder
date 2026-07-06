using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Jobs;
using Jobmatch.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SearchEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Search));
        MapStart(group);
        MapActive(group);
        MapGet(group);
        MapStream(group);
        MapCancel(group);
    }

    private static void MapStart(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Search.Run,
                ([FromServices] IJobSearchHandler handler, [FromBody] SearchRequest? request)
                    => handler.Start(request))
            .WithName($"{nameof(Routes.Search)}.Start")
            .WithSummary("Start search")
            .WithDescription("Enqueues a background search run and returns its id. Progress streams from the {id}/stream endpoint.")
            .Produces<StartSearchResponse>(StatusCodes.Status200OK);
    }

    private static void MapActive(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Search.Active,
                ([FromServices] IJobSearchHandler handler) => handler.Active())
            .WithName($"{nameof(Routes.Search)}.Active")
            .WithSummary("Get active search")
            .WithDescription("Returns the most recent non-terminal run for reconnect after a reload, or null.")
            .Produces<JobSearch>(StatusCodes.Status200OK);
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Search.ById,
                ([FromServices] IJobSearchHandler handler, [FromRoute] string id) => handler.Get(id))
            .WithName($"{nameof(Routes.Search)}.Get")
            .WithSummary("Get search state")
            .WithDescription("Returns the current JobSearch state, phase, provider progress, and timeline.")
            .Produces<JobSearch>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapStream(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Search.Stream,
                ([FromServices] IJobSearchHandler handler, [FromRoute] string id, HttpContext http, CancellationToken ct)
                    => handler.Stream(id, http, ct))
            .WithName($"{nameof(Routes.Search)}.Stream")
            .WithSummary("Stream search progress")
            .WithDescription("Server-Sent Events: the current snapshot then live JobSearch updates. Disconnecting does not cancel the run.");
    }

    private static void MapCancel(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Search.Cancel,
                ([FromServices] IJobSearchHandler handler, [FromRoute] string id) => handler.Cancel(id))
            .WithName($"{nameof(Routes.Search)}.Cancel")
            .WithSummary("Cancel search")
            .WithDescription("Cancels a running or queued background search run.")
            .Produces(StatusCodes.Status200OK);
    }
}
