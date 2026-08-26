using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Applications;

/// <summary>
/// What the user thought of a listing and what happened after they applied. Marks and applications
/// are one feature: an application status and a good/bad mark live in the same record (R-096), and
/// both feed the judge's few-shot examples on later runs.
/// </summary>
public static class ApplicationsModule
{
    public static IServiceCollection AddApplications(this IServiceCollection services)
    {
        services.AddScoped<IMarksService, MarksService>();
        services.AddScoped<IApplicationsService, ApplicationsService>();
        services.AddScoped<IMarksHandler, MarksHandler>();
        services.AddScoped<IApplicationsHandler, ApplicationsHandler>();
        return services;
    }

    public static WebApplication MapApplications(this WebApplication app)
    {
        new MarksEndpoints().Register(app);
        new ApplicationsEndpoints().Register(app);
        return app;
    }
}
