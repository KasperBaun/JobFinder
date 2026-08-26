using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Infrastructure;

/// <summary>
/// What every handler method shares: a log line either side of the operation, and the translation
/// from a service's typed exception to an HTTP response. Handlers wrap their bodies in
/// <see cref="ExecuteAsync(string, Func{Task{IResult}}, object?[])"/> and throw rather than
/// catching, so error mapping is decided in one place.
/// </summary>
public abstract class HandlerBase(ILogger logger)
{
    protected ILogger Logger { get; } = logger;

    /// <param name="operationName">
    /// A message template, e.g. <c>"get provider {ProviderId}"</c>. Placeholders bind to
    /// <paramref name="logParams"/> in order — this is a real template, not an interpolated string,
    /// so structured logging can index on the values.
    /// </param>
    protected async Task<IResult> ExecuteAsync(
        string operationName,
        Func<Task<IResult>> operation,
        params object?[] logParams)
    {
#pragma warning disable CA2254 // The template is the caller's operation name by design.
        Logger.LogInformation("Starting " + operationName, logParams);
        try
        {
            var result = await operation().ConfigureAwait(false);
            Logger.LogInformation("Completed " + operationName, logParams);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed " + operationName, logParams);
            return MapException(ex);
        }
#pragma warning restore CA2254
    }

    protected Task<IResult> ExecuteAsync(string operationName, Func<Task<IResult>> operation)
        => ExecuteAsync(operationName, operation, []);

    private static IResult MapException(Exception ex) => ex switch
    {
        NotFoundException notFound => Results.NotFound(notFound.Message),
        InvalidRequestException invalid => Results.BadRequest(invalid.Message),
        ConflictException conflict => Results.Conflict(conflict.Message),
        ConfigException config => Results.BadRequest(config.Message),
        // First-run setup has not chosen a data directory yet, so there is nothing to read or write.
        // 428 tells the GUI to route to the setup screen rather than showing a failure.
        SetupRequiredException => Results.Json(
            new { setupRequired = true }, statusCode: StatusCodes.Status428PreconditionRequired),
        _ => Results.Problem(detail: ex.Message, statusCode: 500, title: "An unexpected error occurred"),
    };
}
