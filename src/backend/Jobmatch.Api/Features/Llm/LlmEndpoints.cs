using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Features.Llm;

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
            .WithDescription("Returns whether the LLM is enabled, which provider, and whether the bundled GGUF model file is present on disk.")
            .Produces<LlmStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status428PreconditionRequired)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapDownload(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Llm.DownloadModel,
                ([FromServices] ILlmHandler handler) => handler.StartDownload())
            .WithName($"{nameof(Routes.Llm)}.DownloadModel")
            .WithSummary("Start LLM model download")
            .WithDescription("Starts a background download of the GGUF model into the user's data directory, or no-ops if one is already running. Returns the current download state immediately; live progress is reported by GET /api/llm/status, which the client polls.")
            .Produces<LlmDownloadStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status428PreconditionRequired)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
