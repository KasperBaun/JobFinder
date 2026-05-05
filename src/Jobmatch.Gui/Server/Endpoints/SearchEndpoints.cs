using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Endpoints;

public static class SearchEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapPost(Routes.Search.Run,
            (SearchRequest req, Jobmatch.UserContext ctx, HttpContext http, CancellationToken ct) =>
                SearchHandler.Run(req, ctx, http, ct));
}
