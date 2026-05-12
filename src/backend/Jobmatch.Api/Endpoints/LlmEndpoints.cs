using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class LlmEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Llm));
        MapStatus(group);
        MapDownload(group);
    }

    private static void MapStatus(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Llm.Status,
                ([FromServices] ILlmHandler handler) => handler.Status())
            .WithName($"{nameof(Routes.Llm)}.Status")
            .WithSummary("LLM status")
            .WithDescription("Returns whether the LLM is enabled, which provider, and whether the bundled GGUF model file is present on disk.");
    }

    private static void MapDownload(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Llm.DownloadModel,
                ([FromServices] ILlmHandler handler, HttpContext http, CancellationToken ct)
                    => handler.DownloadModel(http, ct))
            .WithName($"{nameof(Routes.Llm)}.DownloadModel")
            .WithSummary("Download LLM model")
            .WithDescription("Streams the GGUF model file from the configured download URL into the user's data directory. SSE progress events; finishes with a complete or error event.");
    }
}
