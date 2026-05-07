using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class WhoamiEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Whoami));
        MapGetWhoami(group);
    }

    private static void MapGetWhoami(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Whoami.Get,
                ([FromServices] IWhoamiHandler handler) => handler.Get())
            .WithName($"{nameof(Routes.Whoami)}.{nameof(Routes.Whoami.Get)}")
            .WithSummary("Get whoami")
            .WithDescription("Returns the active user's email, data directory, and tool version.")
            .Produces<WhoamiResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
