using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Features.Drafting;

public sealed class CvEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Cv));
        MapGet(group);
        MapUpdate(group);
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Cv.Get,
                ([FromServices] IDraftingHandler handler) => handler.GetCv())
            .WithName($"{nameof(Routes.Cv)}.{nameof(Routes.Cv.Get)}")
            .WithSummary("Get the stored CV text")
            .WithDescription("The career facts drafting writes from. Null until a CV has been extracted or saved.")
            .Produces<CvResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapUpdate(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Cv.Update,
                ([FromBody] CvUpdateRequest request, [FromServices] IDraftingHandler handler) => handler.UpdateCv(request))
            .WithName($"{nameof(Routes.Cv)}.{nameof(Routes.Cv.Update)}")
            .WithSummary("Replace the stored CV text")
            .WithDescription("Overwrites cv.md. Whitespace is normalised and the text is truncated to the model's context budget on save.")
            .Produces<CvResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
