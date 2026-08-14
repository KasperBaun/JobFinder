using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Transfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Features.Transfer;

/// <summary>Exporting the user's whole data directory as a zip, and restoring one.</summary>
public static class TransferModule
{
    public static IServiceCollection AddTransfer(this IServiceCollection services)
    {
        services.AddScoped<IConfigTransferService, ConfigTransferService>();
        services.AddScoped<IConfigTransferHandler, ConfigTransferHandler>();
        return services;
    }

    public static WebApplication MapTransfer(this WebApplication app)
    {
        new ConfigTransferEndpoints().Register(app);
        return app;
    }
}
