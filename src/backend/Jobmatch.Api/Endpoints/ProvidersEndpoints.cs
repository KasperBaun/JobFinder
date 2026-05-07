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
}
