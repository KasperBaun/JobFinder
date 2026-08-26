using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Settings;

/// <summary>The interface language. Reads ride along on Setup.Status, which the GUI fetches at boot.</summary>
public static class SettingsModule
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddScoped<ISettingsHandler, SettingsHandler>();
        return services;
    }

    public static WebApplication MapSettings(this WebApplication app)
    {
        new SettingsEndpoints().Register(app);
        return app;
    }
}
