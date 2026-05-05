using System.Reflection;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class WhoamiHandler
{
    public static IResult Get(Jobmatch.UserContext ctx)
    {
        try
        {
            return Results.Ok(new WhoamiResponse(ctx.Email, ctx.RootDir, GetVersion()));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
    }
}
