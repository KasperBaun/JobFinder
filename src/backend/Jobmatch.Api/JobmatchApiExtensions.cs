using Jobmatch.Api.Features.Applications;
using Jobmatch.Api.Features.Health;
using Jobmatch.Api.Features.History;
using Jobmatch.Api.Features.Llm;
using Jobmatch.Api.Features.Providers;
using Jobmatch.Api.Features.Search;
using Jobmatch.Api.Features.Settings;
using Jobmatch.Api.Features.Setup;
using Jobmatch.Api.Features.Skillsets;
using Jobmatch.Api.Features.Transfer;
using Jobmatch.Api.Features.Whoami;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Bootstrap;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Paths;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api;

/// <summary>
/// The composition root. Every feature owns its own registrations under
/// <c>Features/&lt;Name&gt;/&lt;Name&gt;Module.cs</c>, so adding or removing one is a line here
/// rather than an edit in three separate blocks.
/// </summary>
public static class JobmatchApiExtensions
{
    /// <param name="enableBackgroundJobs">
    /// When true (dev + host), registers Hangfire (SQLite storage) and starts the in-process job
    /// server so searches actually run. Tests pass false so no SQLite db is created and no server
    /// thread starts.
    /// </param>
    public static IServiceCollection AddJobmatchApi(this IServiceCollection services, bool enableBackgroundJobs = true)
    {
        services.AddJobmatchJson();
        services.AddActiveUser();

        services.AddHealth();
        services.AddWhoami();
        services.AddSetup();
        services.AddSettings();
        services.AddProviders();
        services.AddSkillsets();
        services.AddSearch(enableBackgroundJobs);
        services.AddHistory();
        services.AddApplications();
        services.AddLlm();
        services.AddTransfer();

        return services;
    }

    /// <summary>
    /// The active user, and the two ambient dependencies that resolving their files needs.
    /// Resolution is deferred through the provider so the app can boot and show a first-run setup
    /// screen (on a machine with no git identity) instead of crashing. The provider loads the
    /// persisted bootstrap config on construction and runs the one-time portals migration.
    /// UserContext itself is scoped, so a Settings profile switch applies to the next request or
    /// job scope instead of leaving services pinned to the first resolved directory.
    /// </summary>
    private static void AddActiveUser(this IServiceCollection services)
    {
        services.AddSingleton<BootstrapStore>(_ => new BootstrapStore());
        services.AddSingleton<IUserContextProvider, UserContextProvider>();
        services.AddScoped<UserContext>(sp => sp.GetRequiredService<IUserContextProvider>().Current);

        // Filesystem abstraction — physical by default; tests stage in-memory.
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();

        // Injectable clock (MarksService stamps status changes with it; tests pin a fixed one).
        services.AddSingleton(TimeProvider.System);
    }

    public static WebApplication MapJobmatchApi(this WebApplication app)
    {
        app.UseSetupRequired();

        app.MapHealth();
        app.MapWhoami();
        app.MapSetup();
        app.MapSettings();
        app.MapProviders();
        app.MapSkillsets();
        app.MapSearch();
        app.MapHistory();
        app.MapApplications();
        app.MapLlm();
        app.MapTransfer();

        return app;
    }
}
