using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;
using Jobmatch.Features.Drafting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Drafting;

/// <summary>
/// Drafting a resume and cover letter for a listing the user is applying to (R-121), and the stored
/// CV it writes from. The CV lives here rather than under Skillsets because drafting is its only
/// consumer — extraction reads a CV to prefill a profile, but never needed to keep one.
/// </summary>
public static class DraftingModule
{
    public static IServiceCollection AddDrafting(this IServiceCollection services)
    {
        services.AddScoped<ICvDocumentStore, CvDocumentStore>();
        services.AddScoped<IApplicationDraftService, ApplicationDraftService>();
        services.AddScoped<IDraftingHandler, DraftingHandler>();
        services.AddSingleton<DraftManager>();
        return services;
    }

    public static WebApplication MapDrafting(this WebApplication app)
    {
        new CvEndpoints().Register(app);
        new DraftingEndpoints().Register(app);
        return app;
    }
}
