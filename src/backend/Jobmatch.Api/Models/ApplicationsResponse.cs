using Jobmatch.Features.Applications;

namespace Jobmatch.Api.Models;

public sealed record ApplicationsResponse(IReadOnlyList<ApplicationEntry> Applications);
