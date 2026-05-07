using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class HistoryEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.History));
        MapList(group);
        MapGetByRunId(group);
        MapDelete(group);
    }

    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.History.GetAll,
                ([FromServices] IHistoryHandler handler) => handler.List())
            .WithName($"{nameof(Routes.History)}.{nameof(Routes.History.GetAll)}")
            .WithSummary("List history runs")
            .WithDescription("Returns a list of past search runs ordered most recent first.")
            .Produces<HistoryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapGetByRunId(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.History.GetByRunId,
                (
                    [FromServices] IHistoryHandler handler,
                    [FromRoute] string runId)
                    => handler.GetByRunId(runId))
            .WithName($"{nameof(Routes.History)}.{nameof(Routes.History.GetByRunId)}")
            .WithSummary("Get run detail")
            .WithDescription("Returns the full detail of a single search run, including marks.")
            .Produces<RunDetail>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapDelete(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.History.Delete,
                (
                    [FromServices] IHistoryHandler handler,
                    [FromBody] DeleteHistoryRequest? request)
                    => handler.Delete(request))
            .WithName($"{nameof(Routes.History)}.{nameof(Routes.History.Delete)}")
            .WithSummary("Delete history runs")
            .WithDescription("Deletes one or more history runs by ID and prunes their marks.")
            .Produces<DeleteHistoryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
