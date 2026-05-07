using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Infrastructure;

public abstract class HandlerBase(ILogger logger)
{
    protected ILogger Logger { get; } = logger;

    protected async Task<IResult> ExecuteAsync(
        string operationName,
        Func<Task<IResult>> operation,
        params object?[] logParams)
    {
        Logger.LogInformation($"Starting {operationName}", logParams);
        try
        {
            var result = await operation().ConfigureAwait(false);
            Logger.LogInformation($"Completed {operationName}", logParams);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error during {operationName}", logParams);
            return MapException(ex);
        }
    }

    protected Task<IResult> ExecuteAsync(string operationName, Func<Task<IResult>> operation)
        => ExecuteAsync(operationName, operation, []);

    private static IResult MapException(Exception ex) => ex switch
    {
        Jobmatch.NotFoundException notFound => Results.NotFound(notFound.Message),
        Jobmatch.InvalidRequestException invalid => Results.BadRequest(invalid.Message),
        Jobmatch.ConflictException conflict => Results.Conflict(conflict.Message),
        Jobmatch.ConfigException config => Results.BadRequest(config.Message),
        _ => Results.Problem(detail: ex.Message, statusCode: 500, title: "An unexpected error occurred"),
    };
}
