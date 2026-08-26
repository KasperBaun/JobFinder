using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.History;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.History;

/// <summary>Past runs and their results.</summary>
public static class HistoryModule
{
    public static IServiceCollection AddHistory(this IServiceCollection services)
    {
        // The only reader and writer of history/<runId>.json — several features read through it.
        services.AddScoped<IRunHistoryStore, RunHistoryStore>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IHistoryHandler, HistoryHandler>();
        return services;
    }

    public static WebApplication MapHistory(this WebApplication app)
    {
        new HistoryEndpoints().Register(app);
        return app;
    }
}
