using Jobmatch.Gui.Server.Handlers;
using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Gui.Server.Endpoints;

public static class SkillsetEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(Routes.Skillset.Get, (Jobmatch.UserContext ctx) => SkillsetHandler.Get(ctx));
}
