using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jobmatch.Api.Endpoints;

public sealed class ProvidersEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("").WithTags(nameof(Routes.Providers));
        MapList(group);
        MapGetById(group);
        MapUpdate(group);
        MapTest(group);
        MapSetSecrets(group);
        MapSetConfig(group);
        MapDetect(group);
        MapPreviewTest(group);
        MapCreate(group);
        MapDelete(group);
    }

    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Providers.GetAll,
                ([FromServices] IProvidersHandler handler) => handler.List())
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.GetAll)}")
            .WithSummary("List providers")
            .WithDescription("Returns the catalogue of job-board providers with per-user enabled state and last-fetch info.")
            .Produces<ProvidersResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapGetById(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Providers.GetById,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id)
                    => handler.GetById(id))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.GetById)}")
            .WithSummary("Get provider")
            .WithDescription("Returns the full detail of a single provider, including the most recent fetch results.")
            .Produces<ProviderDetail>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapUpdate(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Providers.Update,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id,
                    [FromBody] ProviderUpsert? request)
                    => handler.Update(id, request))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.Update)}")
            .WithSummary("Toggle provider enabled state")
            .WithDescription("Toggles whether a provider participates in search runs. Only the Enabled field is read.")
            .Produces<SaveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapTest(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Providers.Test,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id,
                    CancellationToken ct)
                    => handler.Test(id, ct))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.Test)}")
            .WithSummary("Test provider connectivity")
            .WithDescription("Performs a live fetch against the provider's endpoint and reports whether listings were returned.")
            .Produces<ProviderTestResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapSetSecrets(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Providers.SetSecrets,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id,
                    [FromBody] SetSecretsRequest? request)
                    => handler.SetSecrets(id, request))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.SetSecrets)}")
            .WithSummary("Set provider secrets")
            .WithDescription("Persists provider-specific secret values (e.g. API keys) for the active user.")
            .Produces<SaveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapSetConfig(RouteGroupBuilder group)
    {
        group.MapPut(
                Routes.Providers.SetConfig,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id,
                    [FromBody] ProviderConfigUpdate? request)
                    => handler.SetConfig(id, request))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.SetConfig)}")
            .WithSummary("Set provider config override")
            .WithDescription("Persists a per-user override of a source's fetch knobs (max pages, page size, rate limit, body enrichment). An all-null body resets the source to its catalog defaults.")
            .Produces<SaveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapDetect(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Providers.Detect,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromBody] DetectSourceRequest? request)
                    => handler.Detect(request))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.Detect)}")
            .WithSummary("Detect a source from a URL")
            .WithDescription("Recognises known ATS job boards and RSS feeds from a pasted URL and returns addable candidates.")
            .Produces<DetectSourceResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapPreviewTest(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Providers.PreviewTest,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromBody] PreviewSourceRequest? request,
                    CancellationToken ct)
                    => handler.PreviewTest(request, ct))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.PreviewTest)}")
            .WithSummary("Preview-test a detected source")
            .WithDescription("Runs a live fetch against a detected (not-yet-saved) candidate and reports whether listings came back.")
            .Produces<ProviderTestResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapCreate(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Providers.Create,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromBody] CreateSourceRequest? request)
                    => handler.Create(request))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.Create)}")
            .WithSummary("Add a source")
            .WithDescription("Persists a detected or manual source as a new user provider and returns its id.")
            .Produces<ProviderCreatedResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static void MapDelete(RouteGroupBuilder group)
    {
        group.MapDelete(
                Routes.Providers.Delete,
                (
                    [FromServices] IProvidersHandler handler,
                    [FromRoute] int id)
                    => handler.Delete(id))
            .WithName($"{nameof(Routes.Providers)}.{nameof(Routes.Providers.Delete)}")
            .WithSummary("Remove a source")
            .WithDescription("Removes a user-added source. Sources from the shipped catalog cannot be removed.")
            .Produces<SaveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
