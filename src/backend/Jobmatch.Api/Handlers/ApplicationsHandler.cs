using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IApplicationsHandler
{
    Task<IResult> Get();
}

public sealed class ApplicationsHandler(IApplicationsService applications, ILogger<ApplicationsHandler> logger)
    : HandlerBase(logger), IApplicationsHandler
{
    public Task<IResult> Get() => ExecuteAsync(
        "list applications",
        () => Task.FromResult<IResult>(Results.Ok(new ApplicationsResponse(applications.List()))));
}
