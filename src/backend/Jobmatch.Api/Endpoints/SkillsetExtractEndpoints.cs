using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SkillsetExtractEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Skillset));
        MapStart(group);
        MapStatus(group);
    }

    private static void MapStart(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Skillset.Extract,
                (
                    [FromServices] ISkillsetExtractHandler handler,
                    IFormFile? file,
                    [FromForm] string? text,
                    [FromForm] string? url)
                    => handler.Start(file, text, url))
            .WithName($"{nameof(Routes.Skillset)}.{nameof(Routes.Skillset.Extract)}")
            .WithSummary("Extract profile from CV")
            .WithDescription("Starts a background AI extraction of profile fields from a CV (pasted text, uploaded .pdf/.txt/.md file, or URL). The result only prefills the profile form; nothing is saved until the user confirms.")
            .DisableAntiforgery()
            .Produces<CvExtractionStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapStatus(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Skillset.ExtractStatus,
                ([FromServices] ISkillsetExtractHandler handler) => handler.Status())
            .WithName($"{nameof(Routes.Skillset)}.{nameof(Routes.Skillset.ExtractStatus)}")
            .WithSummary("CV extraction status")
            .WithDescription("Returns the state of the current or last CV extraction, including the extracted profile once completed.")
            .Produces<CvExtractionStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
