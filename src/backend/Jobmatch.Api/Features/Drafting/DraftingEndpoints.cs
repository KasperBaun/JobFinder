using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Features.Drafting;

public sealed class DraftingEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Drafting));
        MapDraft(group);
        MapStatus(group);
    }

    private static void MapDraft(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Drafting.Draft,
                ([FromBody] DraftRequest request, [FromServices] IDraftingHandler handler) => handler.Start(request))
            .WithName($"{nameof(Routes.Drafting)}.{nameof(Routes.Drafting.Draft)}")
            .WithSummary("Draft a resume and cover letter for a listing")
            .WithDescription("Starts a background draft against the listing's stored ad text and the user's CV, and returns immediately. Poll the status endpoint for the result. A repeat call for the same listing while one is running observes that run rather than starting a second; a call for a different listing is refused with 409 while one is in flight.")
            .Produces<DraftStatusResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapStatus(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Drafting.Status,
                ([FromServices] IDraftingHandler handler) => handler.Status())
            .WithName($"{nameof(Routes.Drafting)}.{nameof(Routes.Drafting.Status)}")
            .WithSummary("Progress of the current or last draft")
            .WithDescription("Reports the in-flight draft, or the outcome of the most recent one, including the paths of the written documents.")
            .Produces<DraftStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
