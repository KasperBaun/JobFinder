using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;
using Jobmatch.Features.Drafting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Drafting;

/// <summary>
/// Drafting a resume and cover letter for a listing the user is applying to (R-121). The CV endpoints
/// live here because drafting is what makes an editable CV worth exposing, but the store itself is
/// registered by Skillsets, which is where the CV is written.
/// </summary>
public static class DraftingModule
{
    public static IServiceCollection AddDrafting(this IServiceCollection services)
    {
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
