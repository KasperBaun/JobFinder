using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IMarksHandler
{
    Task<IResult> Set(MarkRequest? request);
    Task<IResult> SetStatus(MarkStatusRequest? request);
}

public sealed class MarksHandler(IMarksService marks, ILogger<MarksHandler> logger)
    : HandlerBase(logger), IMarksHandler
{
    public Task<IResult> Set(MarkRequest? request) => ExecuteAsync(
        "set mark: run={RunId} listing={ListingId} mark={Mark}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            marks.Set(request.RunId, request.ListingId, request.Mark, request.Reason);
            return Task.FromResult<IResult>(Results.Ok(new MarkResponse(true)));
        },
        logParams: [request?.RunId, request?.ListingId, request?.Mark]);

    public Task<IResult> SetStatus(MarkStatusRequest? request) => ExecuteAsync(
        "set status: run={RunId} listing={ListingId} status={Status}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            marks.SetStatus(request.RunId, request.ListingId, request.Status);
            return Task.FromResult<IResult>(Results.Ok(new MarkResponse(true)));
        },
        logParams: [request?.RunId, request?.ListingId, request?.Status]);
}
