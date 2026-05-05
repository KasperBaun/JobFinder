using Jobmatch.Gui.Server.Handlers;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class ProvidersEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(Routes.Providers.Get, (Jobmatch.UserContext ctx) => ProvidersHandler.Get(ctx));
}
