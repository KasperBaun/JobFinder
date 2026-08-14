using Jobmatch.Domain.Runs;

namespace Jobmatch.Api.Models;

public sealed record HistoryResponse(IReadOnlyList<RunSummary> Runs);

public sealed record DeleteHistoryRequest(IReadOnlyList<string>? RunIds);

public sealed record DeleteHistoryResponse(int Deleted, IReadOnlyList<string> Missing, string? Error = null);
