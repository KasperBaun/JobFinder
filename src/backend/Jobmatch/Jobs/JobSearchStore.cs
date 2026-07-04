using System.Text.Json;
using Jobmatch.Json;

namespace Jobmatch.Jobs;

public interface IJobSearchStore
{
    void Save(JobSearch job);
    JobSearch? Get(string id);
    IReadOnlyList<JobSearch> List();
    JobSearch? Active();
    int Delete(IReadOnlyList<string> ids);
}

/// <summary>
/// File-backed store for <see cref="JobSearch"/> records under <c>data/&lt;email&gt;/jobsearch/&lt;id&gt;.json</c>.
/// Writes are atomic (temp file + move) so a concurrent SSE replay never reads a half-written file.
/// On read, a non-terminal record whose heartbeat has gone stale (worker presumed dead — e.g. the host
/// was killed mid-run) is surfaced as <see cref="JobSearchState.Interrupted"/> without rewriting the file,
/// so a job that Hangfire later resumes can still take over.
/// </summary>
public sealed class JobSearchStore(UserContext ctx) : IJobSearchStore
{
    private static readonly JsonSerializerOptions WriteOptions = JobmatchJsonOptions.Indented;
    private static readonly JsonSerializerOptions ReadOptions = JobmatchJsonOptions.Default;

    /// <summary>How long without a heartbeat before a non-terminal run is treated as interrupted on read.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    public void Save(JobSearch job)
    {
        Directory.CreateDirectory(ctx.JobSearchDir);
        var path = PathFor(job.Id);
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(job, WriteOptions));

        // Atomic replace, retried: a concurrent reader (SSE replay / history list) can briefly hold the
        // target open, which makes the move fail transiently on Windows.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tmp, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(20);
            }
        }
    }

    public JobSearch? Get(string id)
    {
        var safe = Sanitise(id);
        if (safe is null) return null;
        var job = TryRead(PathFor(safe));
        return job is null ? null : Reconcile(job);
    }

    public IReadOnlyList<JobSearch> List()
    {
        if (!Directory.Exists(ctx.JobSearchDir)) return [];
        var jobs = new List<JobSearch>();
        foreach (var file in Directory.EnumerateFiles(ctx.JobSearchDir, "*.json"))
        {
            var job = TryRead(file);
            if (job is not null) jobs.Add(Reconcile(job));
        }
        return jobs.OrderByDescending(j => j.CreatedAt).ToList();
    }

    public JobSearch? Active() => List().FirstOrDefault(j => !j.IsTerminal);

    public int Delete(IReadOnlyList<string> ids)
    {
        var deleted = 0;
        foreach (var id in ids)
        {
            var safe = Sanitise(id);
            if (safe is null) continue;
            var path = PathFor(safe);
            if (File.Exists(path))
            {
                File.Delete(path);
                deleted++;
            }
        }
        return deleted;
    }

    private static JobSearch Reconcile(JobSearch job)
    {
        if (job.IsTerminal) return job;
        return DateTimeOffset.UtcNow - job.LastHeartbeat > StaleAfter
            ? job.AsInterrupted(DateTimeOffset.UtcNow)
            : job;
    }

    private string PathFor(string id) => Path.Combine(ctx.JobSearchDir, $"{id}.json");

    private static JobSearch? TryRead(string path)
    {
        // Share Delete so a concurrent atomic-replace can proceed; retry transient IO from the race.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<JobSearch>(stream, ReadOptions);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (IOException) when (attempt < 5) { Thread.Sleep(20); }
            catch { return null; }
        }
    }

    private static string? Sanitise(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return id.IndexOfAny(['/', '\\', '.', ':']) >= 0 ? null : id;
    }
}
