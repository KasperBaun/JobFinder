using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class ConfigTransferEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Config));
        MapExport(group);
        MapImport(group);
    }

    private static void MapExport(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Config.Export,
                ([FromServices] IConfigTransferHandler handler) => handler.Export())
            .WithName($"{nameof(Routes.Config)}.{nameof(Routes.Config.Export)}")
            .WithSummary("Export all data")
            .WithDescription("Downloads a zip archive of the active user's complete data directory (excluding the LLM model).")
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapImport(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Config.Import,
                ([FromServices] IConfigTransferHandler handler, IFormFile? file) => handler.Import(file))
            .WithName($"{nameof(Routes.Config)}.{nameof(Routes.Config.Import)}")
            .WithSummary("Import data")
            .WithDescription("Restores a previously exported zip archive, replacing the active user's data (the previous state is backed up first).")
            .DisableAntiforgery()
            .Produces<ImportResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
