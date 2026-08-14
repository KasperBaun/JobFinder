using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Features.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IProvidersHandler
{
    Task<IResult> List();
    Task<IResult> GetById(int id);
    Task<IResult> Update(int id, ProviderUpsert? request);
    Task<IResult> SetSecrets(int id, SetSecretsRequest? request);
    Task<IResult> SetConfig(int id, ProviderConfigUpdate? request);
    Task<IResult> Test(int id, CancellationToken ct);
    Task<IResult> Detect(DetectSourceRequest? request, CancellationToken ct);
    Task<IResult> PreviewTest(PreviewSourceRequest? request, CancellationToken ct);
    Task<IResult> Create(CreateSourceRequest? request, CancellationToken ct);
    Task<IResult> Delete(int id);
}

public sealed class ProvidersHandler(IProvidersService providers, ILogger<ProvidersHandler> logger)
    : HandlerBase(logger), IProvidersHandler
{
    public Task<IResult> List() => ExecuteAsync(
        "list providers",
        () =>
        {
            var summaries = providers.List().Select(ProviderMappings.ToSummary).ToList();
            return Task.FromResult<IResult>(Results.Ok(new ProvidersResponse(summaries)));
        });

    public Task<IResult> GetById(int id) => ExecuteAsync(
        "get provider {ProviderId}",
        () =>
        {
            var detail = providers.GetById(id);
            return Task.FromResult<IResult>(Results.Ok(ProviderMappings.ToDetail(detail)));
        },
        logParams: [id]);

    public Task<IResult> Update(int id, ProviderUpsert? request) => ExecuteAsync(
        "update provider {ProviderId}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            providers.SetEnabled(id, request.Enabled ?? true);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);

    public Task<IResult> SetSecrets(int id, SetSecretsRequest? request) => ExecuteAsync(
        "set provider secrets {ProviderId}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            providers.SetSecrets(id, request.Values);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);

    public Task<IResult> SetConfig(int id, ProviderConfigUpdate? request) => ExecuteAsync(
        "set provider config {ProviderId}",
        () =>
        {
            var ov = new ProviderOverride(
                request?.MaxPages,
                request?.PageSize,
                request?.RateLimitRps,
                request?.EnrichBody);
            providers.SetConfigOverride(id, ov);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);

    public Task<IResult> Test(int id, CancellationToken ct) => ExecuteAsync(
        "test provider {ProviderId}",
        async () =>
        {
            var outcome = await providers.TestAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(ProviderMappings.ToTestResult(outcome));
        },
        logParams: [id]);

    public Task<IResult> Detect(DetectSourceRequest? request, CancellationToken ct) => ExecuteAsync(
        "detect source",
        async () =>
        {
            var candidates = await providers.DetectAsync(request?.Url, ct).ConfigureAwait(false);
            var dtos = candidates
                .Select(ProviderMappings.ToDetected)
                .ToList();
            return Results.Ok(new DetectSourceResponse(dtos));
        });

    public Task<IResult> PreviewTest(PreviewSourceRequest? request, CancellationToken ct) => ExecuteAsync(
        "preview-test source",
        async () =>
        {
            if (request?.Kind is null)
                throw new InvalidRequestException("kind is required");
            var preview = await providers
                .PreviewAsync(request.Url, request.Kind, request.DisplayName, ct)
                .ConfigureAwait(false);
            return Results.Ok(new SourcePreviewResult(ProviderMappings.ToTestResult(preview.Test), ProviderMappings.ToOverlap(preview.Overlap)));
        });

    public Task<IResult> Create(CreateSourceRequest? request, CancellationToken ct) => ExecuteAsync(
        "create source",
        async () =>
        {
            if (request?.Kind is null)
                throw new InvalidRequestException("kind is required");
            var created = await providers
                .CreateAsync(request.Url, request.Kind, request.DisplayName, ct)
                .ConfigureAwait(false);
            return Results.Ok(new ProviderCreatedResponse(created.Portal.Id));
        });

    public Task<IResult> Delete(int id) => ExecuteAsync(
        "delete provider {ProviderId}",
        () =>
        {
            providers.Delete(id);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);
}
