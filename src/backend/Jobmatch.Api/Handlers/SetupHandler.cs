using Jobmatch;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISetupHandler
{
    Task<IResult> Status();
    Task<IResult> Complete(SetupRequest? request);
}

public sealed class SetupHandler(IUserContextProvider provider, ILogger<SetupHandler> logger)
    : HandlerBase(logger), ISetupHandler
{
    public Task<IResult> Status() => ExecuteAsync(
        "get setup status",
        () => Task.FromResult<IResult>(Results.Ok(ToResponse(provider.State()))));

    public Task<IResult> Complete(SetupRequest? request) => ExecuteAsync(
        "complete first-run setup",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("A setup request body is required.");

            provider.Complete(request.Email, request.DataDir);
            return Task.FromResult<IResult>(Results.Ok(ToResponse(provider.State())));
        });

    private static SetupStatusResponse ToResponse(SetupState state) => new(
        Configured: state.IsConfigured,
        ProfileExists: state.ProfileExists,
        Email: state.Email,
        DataDir: state.DataDir,
        SuggestedEmail: state.SuggestedEmail,
        SuggestedDataDir: state.SuggestedDataDir,
        BootstrapPath: state.BootstrapPath);
}
