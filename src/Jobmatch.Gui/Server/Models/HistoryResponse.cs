using Jobmatch.Search;

namespace Jobmatch.Gui.Server.Models;

public sealed record HistoryResponse(IReadOnlyList<RunSummary> Runs);
