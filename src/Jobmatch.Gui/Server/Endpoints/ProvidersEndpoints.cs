using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class ProvidersEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(Routes.Providers.Get, (Jobmatch.UserContext ctx) => ProvidersHandler.Get(ctx));
        app.MapPut(Routes.Providers.Put, (ProvidersUpdateRequest? req, Jobmatch.UserContext ctx) => ProvidersHandler.Put(req, ctx));
    }
}
