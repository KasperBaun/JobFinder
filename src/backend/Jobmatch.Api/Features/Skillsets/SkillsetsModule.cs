using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;
using Jobmatch.Features.Skillsets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Skillsets;

/// <summary>
/// The user's profile: reading and writing it, and filling it in from a CV. The two arrive together
/// because CV extraction exists to produce a profile — its output is the skillset form's input.
/// </summary>
public static class SkillsetsModule
{
    public static IServiceCollection AddSkillsets(this IServiceCollection services)
    {
        services.AddScoped<ISkillsetService, SkillsetService>();
        services.AddScoped<ICvExtractionService, CvExtractionService>();

        // Extraction writes the CV it read, so the store belongs to whoever registers extraction —
        // Drafting reads it later, but it is not the reason the file exists.
        services.AddScoped<ICvDocumentStore, CvDocumentStore>();

        // Save-time DAWA geocoding for the radius filter (R-105). Short timeout on purpose:
        // a slow or offline lookup degrades to a save without coordinates, never a failed save.
        services.AddHttpClient<IGeocodingService, DawaGeocodingService>(c => c.Timeout = TimeSpan.FromSeconds(5));

        // Singleton: a CV extraction takes 30-90s on CPU and must survive the SPA navigating away
        // (the client polls /api/skillset/extract/status to reconnect).
        services.AddSingleton<CvExtractionManager>();

        services.AddScoped<ISkillsetHandler, SkillsetHandler>();
        services.AddScoped<ISkillsetExtractHandler, SkillsetExtractHandler>();
        return services;
    }

    public static WebApplication MapSkillsets(this WebApplication app)
    {
        new SkillsetEndpoints().Register(app);
        new SkillsetExtractEndpoints().Register(app);
        return app;
    }
}
