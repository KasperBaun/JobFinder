using Jobmatch.Services;

namespace Jobmatch.Api.Models;

public sealed record ApplicationsResponse(IReadOnlyList<ApplicationEntry> Applications);
