using Hangfire;
using Hangfire.Storage.SQLite;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Search;

/// <summary>
/// Running a search. The run itself is a Hangfire background job so it survives navigation, reload
/// and a host restart (R-036/R-037/R-038/R-055) — the request only enqueues it and the client
/// follows along over SSE.
/// </summary>
public static class SearchModule
{
    /// <param name="enableBackgroundJobs">
    /// False in the "Testing" environment, so no SQLite db is created and no worker thread starts.
    /// </param>
    public static IServiceCollection AddSearch(this IServiceCollection services, bool enableBackgroundJobs)
    {
        services.AddScoped<ISearchRunner, SearchRunner>();

        // The JobSearch lifecycle store, the live SSE fan-out bus, and the orchestrating service and
        // job. The bus is a singleton (one in-proc broker); the rest are per request or per job scope.
        services.AddScoped<IJobSearchStore, JobSearchStore>();
        services.AddSingleton<JobSearchBus>();
        services.AddScoped<IJobSearchService, JobSearchService>();
        services.AddScoped<SearchJob>();
        services.AddScoped<IJobSearchHandler, JobSearchHandler>();

        if (enableBackgroundJobs)
            services.AddSearchJobServer();

        return services;
    }

    private static void AddSearchJobServer(this IServiceCollection services)
    {
        services.AddHangfire((sp, config) => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSQLiteStorage(
                HangfireStorage.DbPath(sp),
                // Default SQLite poll is ~15s — far too slow for an interactive "Run a search".
                new SQLiteStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(1) }));

        // Registered before the Hangfire server so it runs first at startup: any run left Running by
        // a previous process's exit is re-enqueued to resume promptly (R-036), instead of waiting out
        // Hangfire's SQLite invisibility timeout (~30 min) or lingering as a stuck "running" indicator.
        services.AddHostedService<OrphanedRunResumer>();

        // Single-user tool: one worker serialises runs so two searches don't contend for the LLM.
        services.AddHangfireServer(options => options.WorkerCount = 1);
    }

    public static WebApplication MapSearch(this WebApplication app)
    {
        new SearchEndpoints().Register(app);
        return app;
    }
}
