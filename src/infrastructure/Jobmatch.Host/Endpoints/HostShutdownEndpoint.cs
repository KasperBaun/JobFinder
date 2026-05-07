using Jobmatch.Api;
using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jobmatch.Host.Endpoints;

public sealed class HostShutdownEndpoint : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        app.MapPost(
                Routes.System.Shutdown,
                ([FromServices] CancellationTokenSource shutdownCts) =>
                {
                    shutdownCts.CancelAfter(TimeSpan.FromMilliseconds(300));
                    return Results.Ok();
                })
            .WithTags(Routes.System.Tag)
            .WithName($"{nameof(Routes.System)}.{nameof(Routes.System.Shutdown)}")
            .WithSummary("Shutdown the host process")
            .WithDescription("Host-only. Cancels the host CancellationTokenSource, allowing Kestrel to drain.")
            .Produces(StatusCodes.Status200OK);
    }
}
