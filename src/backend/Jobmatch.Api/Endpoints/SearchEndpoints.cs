using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SearchEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Search));
        MapRun(group);
    }

    private static void MapRun(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Search.Run,
                (
                    [FromServices] ISearchHandler handler,
                    [FromBody] SearchRequest? request,
                    HttpContext http,
                    CancellationToken ct)
                    => handler.Run(request, http, ct))
            .WithName($"{nameof(Routes.Search)}.{nameof(Routes.Search.Run)}")
            .WithSummary("Run search")
            .WithDescription("Streams a search run as Server-Sent Events: provider progress, dedupe, ranking, and final shortlist.");
    }
}
