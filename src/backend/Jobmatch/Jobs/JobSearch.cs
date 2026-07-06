using Jobmatch.Search;

namespace Jobmatch.Jobs;

public enum JobSearchState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Interrupted,
}

public enum JobSearchPhase
{
    Pending,
    Fetching,
    Deduping,
    Ranking,
    LlmJudging,
    Writing,
    Done,
}

/// <summary>Severity of a timeline entry. Serialised as a camelCase string ("info"/"warn"/"error").</summary>
public enum JobSearchEventLevel
{
    Info,
    Warn,
    Error,
}

/// <summary>One timestamped entry in a run's timeline — the "what happened, when" audit shown in the UI.</summary>
public sealed record JobSearchEvent(
    DateTimeOffset Timestamp,
    JobSearchEventLevel Level,
    JobSearchPhase Phase,
    string Message,
    string? Provider = null,
    int? Count = null);

/// <summary>
/// Lifecycle aggregate for a single background search run. The state machine is the source of truth
/// for "what state is this run in"; <see cref="Timeline"/> is the per-run log. Persisted per-id under
/// <c>data/&lt;email&gt;/jobsearch/&lt;id&gt;.json</c>. On success the run also writes the rich
/// <see cref="RunDetail"/> to history under the same id.
/// </summary>
public sealed record JobSearch(
    string Id,
    JobSearchState State,
    JobSearchPhase Phase,
    SearchRequest Request,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<ProviderRunStatus> Providers,
    int FetchedCount,
    int DedupedCount,
    int RankedCount,
    int ShortlistCount,
    double TopScore,
    string? Error,
    string? HangfireJobId,
    int Attempt,
    DateTimeOffset LastHeartbeat,
    IReadOnlyList<JobSearchEvent> Timeline)
{
    public bool IsTerminal => State is JobSearchState.Succeeded
        or JobSearchState.Failed
        or JobSearchState.Cancelled
        or JobSearchState.Interrupted;

    public static JobSearch Create(string id, SearchRequest request, DateTimeOffset now) => new(
        Id: id,
        State: JobSearchState.Queued,
        Phase: JobSearchPhase.Pending,
        Request: request,
        CreatedAt: now,
        StartedAt: null,
        FinishedAt: null,
        Providers: [],
        FetchedCount: 0,
        DedupedCount: 0,
        RankedCount: 0,
        ShortlistCount: 0,
        TopScore: 0.0,
        Error: null,
        HangfireJobId: null,
        Attempt: 0,
        LastHeartbeat: now,
        Timeline: [new JobSearchEvent(now, JobSearchEventLevel.Info, JobSearchPhase.Pending, "Search queued")]);

    public JobSearch WithHangfireJobId(string hangfireJobId) => this with { HangfireJobId = hangfireJobId };

    /// <summary>Append a timeline entry, advance the current phase, and bump the heartbeat. Allowed only while non-terminal.</summary>
    public JobSearch Log(
        JobSearchEventLevel level,
        JobSearchPhase phase,
        string message,
        DateTimeOffset now,
        string? provider = null,
        int? count = null)
    {
        RequireNonTerminal(nameof(Log));
        return AppendLog(level, phase, message, now, provider, count);
    }

    // Unguarded append — used by the terminal transitions to record their own final entry after the
    // state has already flipped to terminal (Log's guard would otherwise reject that).
    private JobSearch AppendLog(
        JobSearchEventLevel level,
        JobSearchPhase phase,
        string message,
        DateTimeOffset now,
        string? provider = null,
        int? count = null) => this with
        {
            Phase = phase,
            LastHeartbeat = now,
            Timeline = [.. Timeline, new JobSearchEvent(now, level, phase, message, provider, count)],
        };

    public JobSearch Heartbeat(DateTimeOffset now) => this with { LastHeartbeat = now };

    public JobSearch WithProviders(IReadOnlyList<ProviderRunStatus> providers) => this with { Providers = providers };

    public JobSearch WithCounts(int? fetched = null, int? deduped = null, int? ranked = null, int? shortlist = null, double? topScore = null) => this with
    {
        FetchedCount = fetched ?? FetchedCount,
        DedupedCount = deduped ?? DedupedCount,
        RankedCount = ranked ?? RankedCount,
        ShortlistCount = shortlist ?? ShortlistCount,
        TopScore = topScore ?? TopScore,
    };

    /// <summary>Queued → Running. Idempotent on Running (a Hangfire retry re-enters Run on an already-running record).</summary>
    public JobSearch MarkRunning(DateTimeOffset now)
    {
        if (State is not (JobSearchState.Queued or JobSearchState.Running))
            throw InvalidTransition(JobSearchState.Running);

        return (this with
        {
            State = JobSearchState.Running,
            Phase = JobSearchPhase.Fetching,
            StartedAt = StartedAt ?? now,
            Attempt = Attempt + 1,
            LastHeartbeat = now,
        }).AppendLog(JobSearchEventLevel.Info, JobSearchPhase.Fetching, Attempt > 0 ? $"Search resumed (attempt {Attempt + 1})" : "Search started", now);
    }

    public JobSearch MarkSucceeded(int shortlistCount, double topScore, DateTimeOffset now)
    {
        RequireNonTerminal(nameof(MarkSucceeded));
        return (this with
        {
            State = JobSearchState.Succeeded,
            Phase = JobSearchPhase.Done,
            FinishedAt = now,
            ShortlistCount = shortlistCount,
            TopScore = topScore,
            LastHeartbeat = now,
        }).AppendLog(JobSearchEventLevel.Info, JobSearchPhase.Done, $"Search complete — {shortlistCount} top jobs", now);
    }

    public JobSearch MarkFailed(string error, DateTimeOffset now)
    {
        RequireNonTerminal(nameof(MarkFailed));
        return (this with
        {
            State = JobSearchState.Failed,
            FinishedAt = now,
            Error = error,
            LastHeartbeat = now,
        }).AppendLog(JobSearchEventLevel.Error, Phase, $"Search failed: {error}", now);
    }

    public JobSearch MarkCancelled(DateTimeOffset now)
    {
        RequireNonTerminal(nameof(MarkCancelled));
        return (this with
        {
            State = JobSearchState.Cancelled,
            FinishedAt = now,
            LastHeartbeat = now,
        }).AppendLog(JobSearchEventLevel.Warn, Phase, "Search cancelled", now);
    }

    /// <summary>Display-only transition for a stale Running/Queued record whose worker is gone (host killed mid-run).</summary>
    public JobSearch AsInterrupted(DateTimeOffset now) => this with
    {
        State = JobSearchState.Interrupted,
        FinishedAt = FinishedAt ?? now,
    };

    private void RequireNonTerminal(string op)
    {
        if (IsTerminal)
            throw new InvalidOperationException($"Cannot {op} on a terminal JobSearch (state={State}).");
    }

    private InvalidOperationException InvalidTransition(JobSearchState target) =>
        new($"Illegal JobSearch transition {State} → {target}.");
}
