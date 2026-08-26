using Jobmatch.Features.Drafting;

namespace Jobmatch.Api.Features.Drafting;

/// <summary>Which listing to draft for. A listing id alone is ambiguous — the ad text lives on a run.</summary>
public sealed record DraftRequest(string RunId, string ListingId);

public sealed record DraftStatusResponse(
    DraftState State,
    string? RunId,
    string? ListingId,
    DateTimeOffset? StartedAt,
    string? Error,
    DraftedDocuments? Result);

public sealed record CvResponse(string? Text);

public sealed record CvUpdateRequest(string Text);
