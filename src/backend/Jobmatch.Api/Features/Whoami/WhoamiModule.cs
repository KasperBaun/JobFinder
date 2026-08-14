using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Whoami;

/// <summary>Who the active user is and where their data lives.</summary>
public static class WhoamiModule
{
    public static IServiceCollection AddWhoami(this IServiceCollection services)
    {
        services.AddScoped<IWhoamiService, WhoamiService>();
        services.AddScoped<IWhoamiHandler, WhoamiHandler>();
        return services;
    }

    public static WebApplication MapWhoami(this WebApplication app)
    {
        new WhoamiEndpoints().Register(app);
        return app;
    }
}
