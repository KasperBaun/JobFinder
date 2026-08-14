using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Setup;

/// <summary>First-run setup: choosing the active user and where their data directory lives.</summary>
public static class SetupModule
{
    public static IServiceCollection AddSetup(this IServiceCollection services)
    {
        services.AddScoped<ISetupHandler, SetupHandler>();
        return services;
    }

    public static WebApplication MapSetup(this WebApplication app)
    {
        new SetupEndpoints().Register(app);
        return app;
    }
}
