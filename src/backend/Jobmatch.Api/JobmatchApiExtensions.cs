using System.Net.Security;
using Jobmatch.Api.Endpoints;
using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Configuration;
using Jobmatch.Llm;
using Jobmatch.Search;
using Jobmatch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api;

public static class JobmatchApiExtensions
{
    public static IServiceCollection AddJobmatchApi(this IServiceCollection services)
    {
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
        services.AddScoped<IProvidersService, ProvidersService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IConfigTransferService, ConfigTransferService>();

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
        services.AddScoped<ISearchHandler, SearchHandler>();
        services.AddScoped<ILlmHandler, LlmHandler>();
        services.AddScoped<IConfigTransferHandler, ConfigTransferHandler>();

        return services;
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
