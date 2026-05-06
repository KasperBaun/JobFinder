using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class SkillsetEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(Routes.Skillset.Get, (Jobmatch.UserContext ctx) => SkillsetHandler.Get(ctx));
        app.MapPut(Routes.Skillset.Put, (SkillsetUpdateRequest? req, Jobmatch.UserContext ctx) => SkillsetHandler.Put(req, ctx));
    }
}
