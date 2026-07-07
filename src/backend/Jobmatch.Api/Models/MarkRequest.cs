namespace Jobmatch.Api.Models;

public sealed record MarkRequest(string RunId, string ListingId, string? Mark, string? Reason = null);

public sealed record MarkStatusRequest(string RunId, string ListingId, string? Status);

public sealed record MarkResponse(bool Success, string? Error = null);
