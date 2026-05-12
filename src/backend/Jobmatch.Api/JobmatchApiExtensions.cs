using System.Net.Security;
using Jobmatch.Api.Endpoints;
using Jobmatch.Api.Handlers;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Configuration;
using Jobmatch.Llm;
using Jobmatch.Search;
using Jobmatch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api;

public static class JobmatchApiExtensions
{
    public static IServiceCollection AddJobmatchApi(this IServiceCollection services)
    {
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
        services.AddScoped<ISearchHandler, SearchHandler>();
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
