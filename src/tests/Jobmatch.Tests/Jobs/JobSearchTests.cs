using Jobmatch.Jobs;
using Jobmatch.Search;

namespace Jobmatch.Tests.Jobs;

public sealed class JobSearchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private static JobSearch NewJob() => JobSearch.Create("20260605-120000-abc123", new SearchRequest(), T0);

    [Fact]
    public void Create_Starts_Queued_With_Pending_Phase_And_A_Timeline_Entry()
    {
        var job = NewJob();
        Assert.Equal(JobSearchState.Queued, job.State);
        Assert.Equal(JobSearchPhase.Pending, job.Phase);
        Assert.False(job.IsTerminal);
        Assert.Single(job.Timeline);
        Assert.Equal(T0, job.CreatedAt);
    }

    [Fact]
    public void MarkRunning_Transitions_To_Running_And_Increments_Attempt()
    {
        var job = NewJob().MarkRunning(T0.AddSeconds(1));
        Assert.Equal(JobSearchState.Running, job.State);
        Assert.Equal(JobSearchPhase.Fetching, job.Phase);
        Assert.Equal(1, job.Attempt);
        Assert.Equal(T0.AddSeconds(1), job.StartedAt);
    }

    [Fact]
    public void MarkRunning_Is_Idempotent_On_Running_And_Counts_Attempts()
    {
        var job = NewJob().MarkRunning(T0.AddSeconds(1)).MarkRunning(T0.AddSeconds(2));
        Assert.Equal(JobSearchState.Running, job.State);
        Assert.Equal(2, job.Attempt);
        // StartedAt is preserved across a resume.
        Assert.Equal(T0.AddSeconds(1), job.StartedAt);
    }

    [Fact]
    public void MarkRunning_Resets_CurrentAttemptStartedAt_On_Each_Resume()
    {
        var first = NewJob().MarkRunning(T0.AddSeconds(1));
        Assert.Equal(T0.AddSeconds(1), first.CurrentAttemptStartedAt);

        // A resume pins CurrentAttemptStartedAt to the new attempt while StartedAt stays on the first.
        var resumed = first.MarkRunning(T0.AddHours(4));
        Assert.Equal(T0.AddHours(4), resumed.CurrentAttemptStartedAt);
        Assert.Equal(T0.AddSeconds(1), resumed.StartedAt);
    }

    [Fact]
    public void Log_Appends_Timeline_Entry_And_Advances_Phase()
    {
        var job = NewJob().MarkRunning(T0).Log(JobSearchEventLevel.Info, JobSearchPhase.Ranking, "rating", T0.AddSeconds(5));
        Assert.Equal(JobSearchPhase.Ranking, job.Phase);
        Assert.Equal("rating", job.Timeline[^1].Message);
        Assert.Equal(JobSearchPhase.Ranking, job.Timeline[^1].Phase);
    }

    [Fact]
    public void MarkSucceeded_Sets_Terminal_State_And_Counts()
    {
        var job = NewJob().MarkRunning(T0).MarkSucceeded(7, 0.91, T0.AddMinutes(1));
        Assert.Equal(JobSearchState.Succeeded, job.State);
        Assert.True(job.IsTerminal);
        Assert.Equal(7, job.ShortlistCount);
        Assert.Equal(0.91, job.TopScore);
        Assert.Equal(JobSearchPhase.Done, job.Phase);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public void MarkFailed_And_MarkCancelled_Are_Terminal()
    {
        Assert.Equal(JobSearchState.Failed, NewJob().MarkRunning(T0).MarkFailed("boom", T0).State);
        Assert.Equal(JobSearchState.Cancelled, NewJob().MarkRunning(T0).MarkCancelled(T0).State);
    }

    [Fact]
    public void Terminal_Job_Rejects_Further_Transitions()
    {
        var done = NewJob().MarkRunning(T0).MarkSucceeded(1, 0.5, T0);
        Assert.Throws<InvalidOperationException>(() => done.MarkFailed("x", T0));
        Assert.Throws<InvalidOperationException>(() => done.MarkCancelled(T0));
        Assert.Throws<InvalidOperationException>(() => done.Log(JobSearchEventLevel.Info, JobSearchPhase.Done, "x", T0));
    }

    [Fact]
    public void AsInterrupted_Marks_Terminal_Without_Throwing()
    {
        var job = NewJob().MarkRunning(T0).AsInterrupted(T0.AddMinutes(10));
        Assert.Equal(JobSearchState.Interrupted, job.State);
        Assert.True(job.IsTerminal);
    }
}
