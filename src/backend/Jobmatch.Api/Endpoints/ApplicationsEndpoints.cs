using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class ApplicationsEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Applications));
        MapGetAll(group);
    }

    private static void MapGetAll(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Applications.GetAll,
                ([FromServices] IApplicationsHandler handler) => handler.Get())
            .WithName($"{nameof(Routes.Applications)}.{nameof(Routes.Applications.GetAll)}")
            .WithSummary("List tracked applications")
            .WithDescription("Aggregates every listing that carries an application status across all runs; when the same listing is statused in several runs, the newest run's status wins.")
            .Produces<ApplicationsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
