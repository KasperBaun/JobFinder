using Jobmatch.Gui.Server.Handlers;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class WhoamiEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(Routes.Whoami.Get, (Jobmatch.UserContext ctx) => WhoamiHandler.Get(ctx));
}
