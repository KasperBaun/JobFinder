namespace Jobmatch.Api.Models;

/// <summary>Returned by POST /api/search — the id of the enqueued background run.</summary>
public sealed record StartSearchResponse(string Id);
