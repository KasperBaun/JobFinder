using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class ProvidersEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(Routes.Providers.Get, (Jobmatch.UserContext ctx) => ProvidersHandler.GetList(ctx));
        app.MapGet(Routes.Providers.GetOne, (int id, Jobmatch.UserContext ctx) => ProvidersHandler.GetOne(id, ctx));
        app.MapPut(Routes.Providers.Update, (int id, ProviderUpsert? req, Jobmatch.UserContext ctx) => ProvidersHandler.Update(id, req, ctx));
        app.MapPost(Routes.Providers.Test, (int id, Jobmatch.UserContext ctx, CancellationToken ct) => ProvidersHandler.Test(id, ctx, ct));
        app.MapPut(Routes.Providers.SetSecrets,
            (int id, SetSecretsRequest? req, Jobmatch.UserContext ctx) => ProvidersHandler.SetSecrets(id, req, ctx));
    }
}
