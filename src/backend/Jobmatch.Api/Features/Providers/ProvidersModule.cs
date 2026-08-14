using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Providers;

/// <summary>
/// The sources a search draws from: the shipped catalog, the ones the user added, and everything
/// that decides what a run fetches from each.
/// </summary>
public static class ProvidersModule
{
    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        // The catalog is what "what are my sources?" resolves to for every caller (R-119).
        services.AddScoped<IProviderCatalog, ProviderCatalog>();
        services.AddScoped<IProvidersService, ProvidersService>();

        // Detection is pure pattern matching; discovery owns one pooled HttpClient behind link
        // discovery, so both are singletons rather than rebuilt per request.
        services.AddSingleton<ISourceDetectionService, SourceDetectionService>();
        services.AddSingleton<ISourceDiscoveryService>(sp =>
            new SourceDiscoveryService(sp.GetRequiredService<ISourceDetectionService>()));

        services.AddScoped<IProvidersHandler, ProvidersHandler>();
        return services;
    }

    public static WebApplication MapProviders(this WebApplication app)
    {
        new ProvidersEndpoints().Register(app);
        return app;
    }
}
