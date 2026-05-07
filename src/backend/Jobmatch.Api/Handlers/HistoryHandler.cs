using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IHistoryHandler
{
    Task<IResult> List();
    Task<IResult> GetByRunId(string runId);
    Task<IResult> Delete(DeleteHistoryRequest? request);
}

public sealed class HistoryHandler(IHistoryService history, ILogger<HistoryHandler> logger)
    : HandlerBase(logger), IHistoryHandler
{
    public Task<IResult> List() => ExecuteAsync(
        "list history runs",
        () =>
        {
            var runs = history.List();
            return Task.FromResult<IResult>(Results.Ok(new HistoryResponse(runs)));
        });

    public Task<IResult> GetByRunId(string runId) => ExecuteAsync(
        "get history run: {RunId}",
        () =>
        {
            var detail = history.GetByRunId(runId);
            return Task.FromResult<IResult>(Results.Ok(detail));
        },
        logParams: [runId]);

    public Task<IResult> Delete(DeleteHistoryRequest? request) => ExecuteAsync(
        "delete history runs",
        () =>
        {
            var ids = request?.RunIds ?? [];
            var result = history.Delete(ids);
            var dto = new DeleteHistoryResponse(result.Deleted, result.Missing);
            return Task.FromResult<IResult>(Results.Ok(dto));
        });
}
