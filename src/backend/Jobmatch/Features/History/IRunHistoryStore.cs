using Jobmatch.Domain.Runs;

namespace Jobmatch.Features.History;

/// <summary>
/// The only reader and writer of <c>history/&lt;runId&gt;.json</c>. Callers that want a run's results
/// go through here rather than composing the path and deserialising themselves, so the file's name
/// and shape are known in exactly one place.
/// </summary>
public interface IRunHistoryStore
{
    void Save(RunDetail detail);

    /// <summary>The run's recorded results, or null if it has none — queued, running, failed, or deleted.</summary>
    RunDetail? Find(string runId);

    /// <summary>Every recorded run, newest first. Unreadable files are skipped, not thrown on.</summary>
    IReadOnlyList<RunDetail> All();

    bool Delete(string runId);
}
