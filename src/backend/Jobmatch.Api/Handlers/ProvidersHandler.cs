using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IProvidersHandler
{
    Task<IResult> List();
    Task<IResult> GetById(int id);
    Task<IResult> Update(int id, ProviderUpsert? request);
    Task<IResult> SetSecrets(int id, SetSecretsRequest? request);
    Task<IResult> Test(int id, CancellationToken ct);
}

public sealed class ProvidersHandler(IProvidersService providers, ILogger<ProvidersHandler> logger)
    : HandlerBase(logger), IProvidersHandler
{
    public Task<IResult> List() => ExecuteAsync(
        "list providers",
        () =>
        {
            var listings = providers.List();
            var summaries = listings.Select(ToSummary).ToList();
            return Task.FromResult<IResult>(Results.Ok(new ProvidersResponse(summaries)));
        });

    public Task<IResult> GetById(int id) => ExecuteAsync(
        "get provider: {ProviderId}",
        () =>
        {
            var detail = providers.GetById(id);
            return Task.FromResult<IResult>(Results.Ok(ToDetail(detail)));
        },
        logParams: [id]);

    public Task<IResult> Update(int id, ProviderUpsert? request) => ExecuteAsync(
        "update provider: {ProviderId}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            providers.SetEnabled(id, request.Enabled ?? true);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);

    public Task<IResult> SetSecrets(int id, SetSecretsRequest? request) => ExecuteAsync(
        "set provider secrets: {ProviderId}",
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            providers.SetSecrets(id, request.Values);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        },
        logParams: [id]);

    public Task<IResult> Test(int id, CancellationToken ct) => ExecuteAsync(
        "test provider: {ProviderId}",
        async () =>
        {
            var outcome = await providers.TestAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(ToTestResult(outcome));
        },
        logParams: [id]);

    private static ProviderSummary ToSummary(ProviderListing l) => new(
        Id: l.Portal.Id,
        Name: l.Portal.Name,
        DisplayName: string.IsNullOrWhiteSpace(l.Portal.DisplayName) ? l.Portal.Name : l.Portal.DisplayName!,
        Type: l.Portal.Type.ToString().ToLowerInvariant(),
        Enabled: l.Enabled,
        Endpoint: l.Portal.Endpoint?.ToString(),
        RateLimitRps: l.Portal.RateLimitRps,
        Notes: l.Portal.Notes,
        LastFetchedAt: l.LastFetchedAt,
        LastFetchCount: l.LastFetchCount,
        RequiresSecret: l.Portal.RequiresSecret,
        HasSecret: l.HasSecret);

    private static ProviderDetail ToDetail(ProviderListingDetail d)
    {
        var l = d.Listing;
        var recent = d.RecentRuns.Select(r => new ProviderRecentRun(
            RunId: r.RunId,
            StartedAt: r.StartedAt,
            Status: r.Status,
            FetchedCount: r.FetchedCount,
            Error: r.Error)).ToList();

        return new ProviderDetail(
            Id: l.Portal.Id,
            Name: l.Portal.Name,
            DisplayName: string.IsNullOrWhiteSpace(l.Portal.DisplayName) ? l.Portal.Name : l.Portal.DisplayName!,
            Type: l.Portal.Type.ToString().ToLowerInvariant(),
            Enabled: l.Enabled,
            Endpoint: l.Portal.Endpoint?.ToString(),
            RateLimitRps: l.Portal.RateLimitRps,
            Notes: l.Portal.Notes,
            LastFetchedAt: l.LastFetchedAt,
            LastFetchCount: l.LastFetchCount,
            RequiresSecret: l.Portal.RequiresSecret,
            HasSecret: l.HasSecret,
            RecentRuns: recent);
    }

    private static ProviderTestResult ToTestResult(ProviderTestOutcome o) => new(
        Ok: o.Ok,
        FetchedCount: o.FetchedCount,
        DurationMs: o.DurationMs,
        SampleTitle: o.SampleTitle,
        Error: o.Error,
        TestedAt: o.TestedAt);
}
