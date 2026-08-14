using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;

namespace Jobmatch.Api.Features.Skillsets;

public sealed record CvExtractionStatusResponse(
    CvExtractionState State,
    DateTimeOffset? StartedAt,
    string? Error,
    ExtractedProfile? Profile);
