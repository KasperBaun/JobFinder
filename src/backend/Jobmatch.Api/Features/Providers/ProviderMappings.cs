using Jobmatch.Features.Providers;

namespace Jobmatch.Api.Features.Providers;

/// <summary>Domain provider types → their wire shapes. Mapping only: what "removable" means, how
/// an override resolves against the catalog default and where a source's search terms live are all
/// decided in <see cref="Jobmatch.Features.Providers"/>.</summary>
internal static class ProviderMappings
{
    public static ProviderSummary ToSummary(ProviderListing l) => new(
        Id: l.Portal.Id,
        Name: l.Portal.Name,
        DisplayName: l.DisplayName,
        Type: l.Portal.Type.ToString().ToLowerInvariant(),
        Enabled: l.Enabled,
        Endpoint: l.Portal.Endpoint?.ToString(),
        RateLimitRps: l.Portal.RateLimitRps,
        Notes: l.Portal.Notes,
        NotesDa: l.Portal.NotesDa,
        LastFetchedAt: l.LastFetchedAt,
        LastFetchCount: l.LastFetchCount,
        RequiresSecret: l.Portal.RequiresSecret,
        HasSecret: l.HasSecret,
        Removable: l.Removable);

    public static ProviderDetail ToDetail(ProviderListingDetail d)
    {
        var l = d.Listing;
        return new ProviderDetail(
            Id: l.Portal.Id,
            Name: l.Portal.Name,
            DisplayName: l.DisplayName,
            Type: l.Portal.Type.ToString().ToLowerInvariant(),
            Enabled: l.Enabled,
            Endpoint: l.Portal.Endpoint?.ToString(),
            RateLimitRps: l.Portal.RateLimitRps,
            Notes: l.Portal.Notes,
            NotesDa: l.Portal.NotesDa,
            LastFetchedAt: l.LastFetchedAt,
            LastFetchCount: l.LastFetchCount,
            RequiresSecret: l.Portal.RequiresSecret,
            HasSecret: l.HasSecret,
            Removable: l.Removable,
            RecentRuns: [.. d.RecentRuns.Select(r => new ProviderRecentRun(
                RunId: r.RunId,
                StartedAt: r.StartedAt,
                Status: r.Status,
                FetchedCount: r.FetchedCount,
                Error: r.Error))],
            Config: ToConfigDto(ProviderFetchSettingsResolver.Resolve(l.Portal, d.Override)));
    }

    public static ProviderConfigDto ToConfigDto(ProviderFetchSettings s) => new(
        Method: s.Method,
        EnrichBody: s.EnrichBody,
        Paginates: s.Paginates,
        MaxPages: s.MaxPages,
        PageSize: s.PageSize,
        HardCeiling: s.HardCeiling,
        SearchQuery: s.SearchQuery,
        RateLimitRps: s.RateLimitRps,
        Defaults: new ProviderConfigDefaults(
            s.Defaults.MaxPages, s.Defaults.PageSize, s.Defaults.RateLimitRps, s.Defaults.EnrichBody),
        RateLimitOverridden: s.RateLimitOverridden,
        EnrichBodyOverridden: s.EnrichBodyOverridden,
        MaxPagesOverridden: s.MaxPagesOverridden,
        PageSizeOverridden: s.PageSizeOverridden);

    public static SourceOverlapDto? ToOverlap(SourceOverlapMatch? m) => m is null
        ? null
        : new SourceOverlapDto(m.ProviderId, m.DisplayName, m.ExistingCount, m.SharedCount, m.Ratio, m.Duplicate);

    public static ProviderTestResult ToTestResult(ProviderTestOutcome o) => new(
        Ok: o.Ok,
        FetchedCount: o.FetchedCount,
        DurationMs: o.DurationMs,
        SampleTitle: o.SampleTitle,
        Error: o.Error,
        TestedAt: o.TestedAt,
        Samples: [.. o.Samples.Select(s => new ProviderTestSampleDto(s.Title, s.Company, s.Location, s.Url))],
        HitPageCap: o.HitPageCap,
        PossiblyCapped: o.PossiblyCapped);

    public static DetectedSourceDto ToDetected(DetectedSource c) => new(c.Kind, c.DisplayName, c.Summary);
}
