using Jobmatch.Search;

namespace Jobmatch.Services;

public sealed record HistoryDeleteResult(int Deleted, IReadOnlyList<string> Missing);

public interface IHistoryService
{
    IReadOnlyList<RunSummary> List();
    RunDetail GetByRunId(string runId);
    HistoryDeleteResult Delete(IReadOnlyList<string> runIds);
}
