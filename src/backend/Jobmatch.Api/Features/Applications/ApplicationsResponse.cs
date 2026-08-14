using Jobmatch.Features.Applications;

namespace Jobmatch.Api.Features.Applications;

public sealed record ApplicationsResponse(IReadOnlyList<ApplicationEntry> Applications);
