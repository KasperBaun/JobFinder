using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Health;

/// <summary>The heartbeat the desktop shell waits on before showing its window.</summary>
public static class HealthModule
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddScoped<ISystemHandler, SystemHandler>();
        return services;
    }

    public static WebApplication MapHealth(this WebApplication app)
    {
        new SystemEndpoints().Register(app);
        return app;
    }
}
