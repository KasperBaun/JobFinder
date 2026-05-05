using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class MarksEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapPost(Routes.Marks.Set, (MarkRequest req, Jobmatch.UserContext ctx) => MarksHandler.Set(req, ctx));
}
