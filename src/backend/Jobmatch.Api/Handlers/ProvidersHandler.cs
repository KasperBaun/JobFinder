using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Configuration;
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
    Task<IResult> SetConfig(int id, ProviderConfigUpdate? request);
    Task<IResult> Test(int id, CancellationToken ct);
    Task<IResult> Detect(DetectSourceRequest? request);
    Task<IResult> PreviewTest(PreviewSourceRequest? request, CancellationToken ct);
    Task<IResult> Create(CreateSourceRequest? request);
    Task<IResult> Delete(int id);
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

    public Task<IResult> SetConfig(int id, ProviderConfigUpdate? request) => ExecuteAsync(
        "set provider config: {ProviderId}",
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
        "test provider: {ProviderId}",
        async () =>
        {
            var outcome = await providers.TestAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(ToTestResult(outcome));
        },
        logParams: [id]);

    public Task<IResult> Detect(DetectSourceRequest? request) => ExecuteAsync(
        "detect source",
        () =>
        {
            var candidates = providers.Detect(request?.Url);
            var dtos = candidates
                .Select(c => new DetectedSourceDto(c.Kind, c.DisplayName, c.Summary, c.DuplicateWarning))
                .ToList();
            return Task.FromResult<IResult>(Results.Ok(new DetectSourceResponse(dtos)));
        });

    public Task<IResult> PreviewTest(PreviewSourceRequest? request, CancellationToken ct) => ExecuteAsync(
        "preview-test source",
        async () =>
        {
            if (request?.Kind is null)
                throw new InvalidRequestException("kind is required");
            var outcome = await providers
                .PreviewTestAsync(request.Url, request.Kind, request.DisplayName, ct)
                .ConfigureAwait(false);
            return Results.Ok(ToTestResult(outcome));
        });

    public Task<IResult> Create(CreateSourceRequest? request) => ExecuteAsync(
        "create source",
        () =>
        {
            if (request?.Kind is null)
                throw new InvalidRequestException("kind is required");
            var created = providers.Create(request.Url, request.Kind, request.DisplayName);
            return Task.FromResult<IResult>(Results.Ok(new ProviderCreatedResponse(created.Portal.Id)));
        });

    public Task<IResult> Delete(int id) => ExecuteAsync(
        "delete provider: {ProviderId}",
        () =>
        {
            providers.Delete(id);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
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
        HasSecret: l.HasSecret,
        Removable: l.Portal.Id >= UserProviderStore.IdBase);

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
            Removable: l.Portal.Id >= UserProviderStore.IdBase,
            RecentRuns: recent,
            Config: ToConfigDto(l.Portal, d.Override));
    }

    private static readonly string[] QueryKeys = ["q", "query", "keywords", "search", "searchText"];

    private static ProviderConfigDto ToConfigDto(PortalConfig portal, ProviderOverride? ov)
    {
        var pg = portal.Pagination;
        var defaultMaxPages = pg?.MaxPages;
        var defaultPageSize = pg?.Size;
        var maxPages = ov?.MaxPages ?? defaultMaxPages;
        var pageSize = ov?.PageSize ?? defaultPageSize;
        var ceiling = maxPages is int mp && pageSize is int ps ? mp * ps : (int?)null;

        return new ProviderConfigDto(
            Method: portal.Method,
            EnrichBody: ov?.EnrichBody ?? portal.EnrichBody,
            Paginates: pg is not null,
            MaxPages: maxPages,
            PageSize: pageSize,
            HardCeiling: ceiling,
            SearchQuery: ExtractSearchQuery(portal.QueryParams),
            RateLimitRps: ov?.RateLimitRps ?? portal.RateLimitRps,
            Defaults: new ProviderConfigDefaults(defaultMaxPages, defaultPageSize, portal.RateLimitRps, portal.EnrichBody),
            RateLimitOverridden: ov?.RateLimitRps is not null,
            EnrichBodyOverridden: ov?.EnrichBody is not null,
            MaxPagesOverridden: ov?.MaxPages is not null,
            PageSizeOverridden: ov?.PageSize is not null);
    }

    private static string? ExtractSearchQuery(IReadOnlyDictionary<string, object?>? queryParams)
    {
        if (queryParams is null) return null;
        foreach (var key in QueryKeys)
        {
            if (queryParams.TryGetValue(key, out var val) && val is not null)
            {
                var s = Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        return null;
    }

    private static ProviderTestResult ToTestResult(ProviderTestOutcome o) => new(
        Ok: o.Ok,
        FetchedCount: o.FetchedCount,
        DurationMs: o.DurationMs,
        SampleTitle: o.SampleTitle,
        Error: o.Error,
        TestedAt: o.TestedAt,
        Samples: [.. o.Samples.Select(s => new ProviderTestSampleDto(s.Title, s.Company, s.Location, s.Url))],
        HitPageCap: o.HitPageCap,
        PossiblyCapped: o.PossiblyCapped);
}
