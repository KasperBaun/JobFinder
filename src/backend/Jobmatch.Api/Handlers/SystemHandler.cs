using Jobmatch.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISystemHandler
{
    Task<IResult> Ping();
}

public sealed class SystemHandler(ILogger<SystemHandler> logger)
    : HandlerBase(logger), ISystemHandler
{
    public Task<IResult> Ping() => Task.FromResult<IResult>(Results.Ok());
}
