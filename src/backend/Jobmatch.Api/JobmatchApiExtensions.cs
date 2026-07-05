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
using Microsoft.AspNetCore.Http;
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

        // Active user — resolution is deferred through the provider so the app can boot and show a
        // first-run setup screen (on a machine with no git identity) instead of crashing. The provider
        // loads the persisted bootstrap config on construction and runs the one-time portals migration.
        services.AddSingleton<BootstrapStore>(_ => new BootstrapStore());
        services.AddSingleton<IUserContextProvider, UserContextProvider>();
        services.AddSingleton<UserContext>(sp => sp.GetRequiredService<IUserContextProvider>().Current);

        // Filesystem abstraction — physical by default; tests stage in-memory.
        services.AddSingleton<Jobmatch.IO.IFileSystem, Jobmatch.IO.PhysicalFileSystem>();

        // Domain services
        services.AddScoped<IWhoamiService, WhoamiService>();
        services.AddScoped<IMarksService, MarksService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<ISkillsetService, SkillsetService>();
        services.AddSingleton<ISourceDetectionService, SourceDetectionService>();
        services.AddScoped<IProvidersService, ProvidersService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IConfigTransferService, ConfigTransferService>();

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
                    HangfireDbPath(sp),
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
        services.AddScoped<ISetupHandler, SetupHandler>();
        services.AddScoped<ISystemHandler, SystemHandler>();
        services.AddScoped<IWhoamiHandler, WhoamiHandler>();
        services.AddScoped<IMarksHandler, MarksHandler>();
        services.AddScoped<IHistoryHandler, HistoryHandler>();
        services.AddScoped<ISkillsetHandler, SkillsetHandler>();
        services.AddScoped<IProvidersHandler, ProvidersHandler>();
        services.AddScoped<IJobSearchHandler, JobSearchHandler>();
        services.AddScoped<ILlmHandler, LlmHandler>();
        services.AddScoped<IConfigTransferHandler, ConfigTransferHandler>();

        return services;
    }

    // hangfire.db is transient job-queue infrastructure that Hangfire opens at server start — before
    // first-run setup may have chosen a data directory. Use the configured data dir when available,
    // else a stable per-user fallback, so the job server starts without forcing the deferred
    // UserContext (which throws SetupRequiredException until setup completes).
    private static string HangfireDbPath(IServiceProvider sp)
    {
        var provider = sp.GetRequiredService<IUserContextProvider>();
        if (provider.IsConfigured)
            return Path.Combine(provider.Current.RootDir, "hangfire.db");

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jobfinder");
        Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, "hangfire.db");
    }

    public static WebApplication MapJobmatchApi(this WebApplication app)
    {
        // Translate "setup not done yet" into 428 so a stray data call while unconfigured returns a
        // clean signal (the GUI gates on /api/setup/status, so this is defence-in-depth).
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (SetupRequiredException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
                await context.Response.WriteAsJsonAsync(new { setupRequired = true });
            }
        });

        IEndpointRegistration[] registrations =
        [
            new SetupEndpoints(),
            new SystemEndpoints(),
            new WhoamiEndpoints(),
            new MarksEndpoints(),
            new HistoryEndpoints(),
            new SkillsetEndpoints(),
            new ProvidersEndpoints(),
            new SearchEndpoints(),
            new LlmEndpoints(),
            new ConfigTransferEndpoints(),
        ];

        foreach (var registration in registrations)
        {
            registration.Register(app);
        }

        return app;
    }
}
