namespace Jobmatch.Gui.Server.Models;

public sealed record DeleteHistoryRequest(IReadOnlyList<string>? RunIds);

public sealed record DeleteHistoryResponse(int Deleted, IReadOnlyList<string> Missing, string? Error = null);
