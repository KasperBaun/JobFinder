using Jobmatch;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISettingsHandler
{
    Task<IResult> SetLanguage(SetLanguageRequest? request);
}

public sealed class SettingsHandler(IUserContextProvider provider, ILogger<SettingsHandler> logger)
    : HandlerBase(logger), ISettingsHandler
{
    public Task<IResult> SetLanguage(SetLanguageRequest? request) => ExecuteAsync(
        "set interface language",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("A language request body is required.");

            var language = provider.SetLanguage(request.Language);
            return Task.FromResult<IResult>(Results.Ok(new LanguageResponse(language)));
        });
}
