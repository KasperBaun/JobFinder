using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Api.Infrastructure;

/// <summary>
/// Translates "first-run setup has not happened yet" into 428 for anything that escapes a handler.
/// HandlerBase already maps the exception, so this is defence in depth for the paths that do not go
/// through a handler at all — the SSE stream, and any middleware that resolves the user context.
/// </summary>
public static class SetupRequiredMiddleware
{
    public static WebApplication UseSetupRequired(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (SetupRequiredException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
                await context.Response.WriteAsJsonAsync(new { setupRequired = true });
            }
        });

        return app;
    }
}
