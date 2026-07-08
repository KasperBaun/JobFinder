using Jobmatch.Api.Infrastructure;
using Jobmatch.Models;

namespace Jobmatch.Api.Models;

public sealed record CvExtractionStatusResponse(
    CvExtractionState State,
    DateTimeOffset? StartedAt,
    string? Error,
    ExtractedProfile? Profile);
