using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IWhoamiHandler
{
    Task<IResult> Get();
}

public sealed class WhoamiHandler(IWhoamiService whoami, ILogger<WhoamiHandler> logger)
    : HandlerBase(logger), IWhoamiHandler
{
    public Task<IResult> Get() => ExecuteAsync(
        "get whoami",
        () =>
        {
            var info = whoami.Get();
            var dto = new WhoamiResponse(info.Email, info.DataDir, info.ToolVersion);
            return Task.FromResult<IResult>(Results.Ok(dto));
        });
}
