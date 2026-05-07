using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SystemEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.System));
        MapPing(group);
    }

    private static void MapPing(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.System.Ping,
                ([FromServices] ISystemHandler handler) => handler.Ping())
            .WithName($"{nameof(Routes.System)}.{nameof(Routes.System.Ping)}")
            .WithSummary("Heartbeat")
            .WithDescription("Returns 200 OK. Clients poll this to detect server disconnect.")
            .Produces(StatusCodes.Status200OK);
    }
}
