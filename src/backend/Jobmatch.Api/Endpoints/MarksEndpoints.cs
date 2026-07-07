using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class MarksEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Marks));
        MapSetMark(group);
    }

    private static void MapSetMark(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Marks.Set,
                (
                    [FromServices] IMarksHandler handler,
                    [FromBody] MarkRequest? request)
                    => handler.Set(request))
            .WithName($"{nameof(Routes.Marks)}.{nameof(Routes.Marks.Set)}")
            .WithSummary("Set listing mark")
            .WithDescription("Marks a listing within a run as 'good', 'bad', or null (cleared), with an optional free-form reason that feeds the LLM judge on later runs.")
            .Produces<MarkResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
