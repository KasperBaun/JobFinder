using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SkillsetEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Skillset));
        MapGet(group);
        MapUpdate(group);
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Skillset.Get,
                ([FromServices] ISkillsetHandler handler) => handler.Get())
            .WithName($"{nameof(Routes.Skillset)}.{nameof(Routes.Skillset.Get)}")
            .WithSummary("Get skillset")
            .WithDescription("Returns the active user's skillset profile (parsed from skillset.md).")
            .Produces<SkillsetResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapUpdate(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Skillset.Update,
                (
                    [FromServices] ISkillsetHandler handler,
                    [FromBody] SkillsetUpdateRequest? request)
                    => handler.Update(request))
            .WithName($"{nameof(Routes.Skillset)}.{nameof(Routes.Skillset.Update)}")
            .WithSummary("Update skillset")
            .WithDescription("Validates and persists changes to the user's skillset profile.")
            .Produces<SaveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
