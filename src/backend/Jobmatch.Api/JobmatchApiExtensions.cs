using System.Net.Security;
using Hangfire;
using Hangfire.Storage.SQLite;
using Jobmatch.Api.Endpoints;
using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Jobs;
using Jobmatch.Configuration;
using Jobmatch.Jobs;
using Jobmatch.Llm;
using Jobmatch.Search;
using Jobmatch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api;

public static class JobmatchApiExtensions
{
    /// <param name="enableBackgroundJobs">
    /// When true (dev + host), registers Hangfire (SQLite storage) and starts the in-process job server so
    /// searches actually run. Tests pass false so no SQLite db is created and no server thread starts.
    /// </param>
    public static IServiceCollection AddJobmatchApi(this IServiceCollection services, bool enableBackgroundJobs = true)
    {
        // Minimal-API JSON must match the SSE / on-disk shape: camelCase, enums as camelCase strings
        // (so JobSearchState/Phase serialise as "running"/"llmJudging", not 4), and nulls omitted.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        });

        // Active user — singleton resolved once per process. Side-effect: runs the one-time portals
        // migration so the host doesn't need to know about it.
        services.AddSingleton<UserContext>(_ =>
        {
            var ctx = UserContext.Resolve();
            PortalsMigrationShim.RunIfNeeded(ctx.RootDir);
            return ctx;
        });

        // Filesystem abstraction — physical by default; tests stage in-memory.
        services.AddSingleton<Jobmatch.IO.IFileSystem, Jobmatch.IO.PhysicalFileSystem>();

        // Domain services
        services.AddScoped<IWhoamiService, WhoamiService>();
        services.AddScoped<IMarksService, MarksService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<ISkillsetService, SkillsetService>();
        services.AddScoped<IProvidersService, ProvidersService>();
        services.AddScoped<ISearchService, SearchService>();

        // Background job search: the JobSearch lifecycle store, the live SSE fan-out bus, and the
        // orchestrating service/job. The bus is a singleton (one in-proc broker); the store and service
        // are scoped (resolved per request and per Hangfire job scope).
        services.AddScoped<IJobSearchStore, JobSearchStore>();
        services.AddSingleton<JobSearchBus>();
        services.AddScoped<IJobSearchService, JobSearchService>();
        services.AddScoped<SearchJob>();

        if (enableBackgroundJobs)
        {
            services.AddHangfire((sp, config) => config
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSQLiteStorage(
                    Path.Combine(sp.GetRequiredService<UserContext>().RootDir, "hangfire.db"),
                    // Default SQLite poll is ~15s — far too slow for an interactive "Run a search".
                    new SQLiteStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(1) }));

            // Single-user tool: one worker serialises runs so two searches don't contend for the LLM.
            services.AddHangfireServer(options => options.WorkerCount = 1);
        }

        // LLM model downloader — singleton because it owns a process-wide lock
        // around the download. HttpClient injected from IHttpClientFactory so we
        // get a long timeout suitable for the multi-GB stream. AllowRenegotiation
        // is required because huggingface.co requests TLS renegotiation during
        // the first hop; .NET's default SocketsHttpHandler refuses it.
        services
            .AddHttpClient<LlmModelDownloader>(c => c.Timeout = TimeSpan.FromHours(2))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    AllowRenegotiation = true,
                },
            });

        // Handlers
        services.AddScoped<ISystemHandler, SystemHandler>();
        services.AddScoped<IWhoamiHandler, WhoamiHandler>();
        services.AddScoped<IMarksHandler, MarksHandler>();
        services.AddScoped<IHistoryHandler, HistoryHandler>();
        services.AddScoped<ISkillsetHandler, SkillsetHandler>();
        services.AddScoped<IProvidersHandler, ProvidersHandler>();
        services.AddScoped<IJobSearchHandler, JobSearchHandler>();
        services.AddScoped<ILlmHandler, LlmHandler>();

        return services;
    }

    public static WebApplication MapJobmatchApi(this WebApplication app)
    {
        IEndpointRegistration[] registrations =
        [
            new SystemEndpoints(),
            new WhoamiEndpoints(),
            new MarksEndpoints(),
            new HistoryEndpoints(),
            new SkillsetEndpoints(),
            new ProvidersEndpoints(),
            new SearchEndpoints(),
            new LlmEndpoints(),
        ];

        foreach (var registration in registrations)
        {
            registration.Register(app);
        }

        return app;
    }
}
