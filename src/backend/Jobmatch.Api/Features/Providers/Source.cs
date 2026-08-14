namespace Jobmatch.Api.Features.Providers;

public sealed record DetectSourceRequest(string? Url);

public sealed record DetectedSourceDto(
    string Kind,
    string DisplayName,
    string Summary);

public sealed record DetectSourceResponse(IReadOnlyList<DetectedSourceDto> Candidates);

public sealed record PreviewSourceRequest(string? Url, string? Kind, string? DisplayName);

/// <summary>A source the candidate turned out to duplicate. <see cref="Ratio"/> is the share of the
/// smaller job set the two have in common; <see cref="Duplicate"/> marks a 1-to-1 match.</summary>
public sealed record SourceOverlapDto(
    int ProviderId,
    string DisplayName,
    int ExistingCount,
    int SharedCount,
    double Ratio,
    bool Duplicate);

/// <summary>The preview a candidate earns by being run: what it fetched, and whether the user
/// already has a source returning the same jobs.</summary>
public sealed record SourcePreviewResult(ProviderTestResult Test, SourceOverlapDto? Overlap);

public sealed record CreateSourceRequest(string? Url, string? Kind, string? DisplayName);

public sealed record ProviderCreatedResponse(int Id);
