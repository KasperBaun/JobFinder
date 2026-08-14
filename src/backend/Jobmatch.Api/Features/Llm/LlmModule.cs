using System.Net.Security;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Pipeline.Llm;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Llm;

/// <summary>The local AI model: where it is, whether it is present, and downloading it.</summary>
public static class LlmModule
{
    public static IServiceCollection AddLlm(this IServiceCollection services)
    {
        services.AddScoped<ILlmModelLocator, LlmModelLocator>();

        // Long timeout for a multi-GB stream. AllowRenegotiation is required because huggingface.co
        // requests TLS renegotiation during the first hop, which .NET's default handler refuses.
        services
            .AddHttpClient<LlmModelDownloader>(c => c.Timeout = TimeSpan.FromHours(2))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions { AllowRenegotiation = true },
            });

        // Singleton so the in-flight download's live progress outlives the request that started it
        // (the SPA polls /api/llm/status to reconnect after navigation/reload).
        services.AddSingleton<ModelDownloadManager>();

        services.AddScoped<ILlmHandler, LlmHandler>();
        return services;
    }

    public static WebApplication MapLlm(this WebApplication app)
    {
        new LlmEndpoints().Register(app);
        return app;
    }
}
