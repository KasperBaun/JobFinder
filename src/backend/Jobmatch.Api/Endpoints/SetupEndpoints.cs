using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SetupEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Setup));
        MapStatus(group);
        MapComplete(group);
    }

    private static void MapStatus(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Setup.Status,
                ([FromServices] ISetupHandler handler) => handler.Status())
            .WithName($"{nameof(Routes.Setup)}.{nameof(Routes.Setup.Status)}")
            .WithSummary("First-run setup status")
            .WithDescription("Reports whether a data location has been chosen, plus suggested defaults for the setup screen.")
            .Produces<SetupStatusResponse>(StatusCodes.Status200OK);
    }

    private static void MapComplete(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Setup.Complete,
                ([FromServices] ISetupHandler handler, [FromBody] SetupRequest? request) => handler.Complete(request))
            .WithName($"{nameof(Routes.Setup)}.{nameof(Routes.Setup.Complete)}")
            .WithSummary("Complete first-run setup")
            .WithDescription("Records the user's chosen data folder and identity, creating and seeding the folder.")
            .Produces<SetupStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
