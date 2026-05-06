using Jobmatch.Gui.Server.Handlers;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class HistoryEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(Routes.History.List, (Jobmatch.UserContext ctx) => HistoryHandler.List(ctx));
        app.MapGet(Routes.History.Detail, (string runId, Jobmatch.UserContext ctx) => HistoryHandler.Detail(runId, ctx));
        app.MapPost(Routes.History.Delete, (Models.DeleteHistoryRequest? req, Jobmatch.UserContext ctx) => HistoryHandler.Delete(req, ctx));
    }
}
