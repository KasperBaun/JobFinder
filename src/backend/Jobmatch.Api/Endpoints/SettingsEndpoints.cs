using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class SettingsEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Settings));
        MapSetLanguage(group);
    }

    private static void MapSetLanguage(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Settings.SetLanguage,
                ([FromServices] ISettingsHandler handler, [FromBody] SetLanguageRequest? request) =>
                    handler.SetLanguage(request))
            .WithName($"{nameof(Routes.Settings)}.{nameof(Routes.Settings.SetLanguage)}")
            .WithSummary("Set the interface language")
            .WithDescription("Persists the GUI language ('en' or 'da') so it survives a restart and applies in both the browser and desktop shells.")
            .Produces<LanguageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
