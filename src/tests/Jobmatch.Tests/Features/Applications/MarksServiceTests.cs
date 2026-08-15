using Jobmatch.Features.Applications;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Features.Applications;

public sealed class MarksServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.Parse("2026-07-01T10:00:00+00:00"));
    private readonly MarksService _marks;

    public MarksServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-marks-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "marks@example.com", repoRoot: _tempRoot, seedExamples: false);
        _marks = new MarksService(_ctx, _clock);
    }

    internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Set_WithReason_RoundTrips()
    {
        _marks.Set("run-1", "l1", "bad", "I'm not a student");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal("bad", mark.Mark);
        Assert.Equal("I'm not a student", mark.Reason);
    }

    [Fact]
    public void Set_WithoutReason_WritesLegacyStringShape()
    {
        _marks.Set("run-1", "l1", "good", null);

        var json = File.ReadAllText(_ctx.MarksPath);
        Assert.Contains("\"l1\": \"good\"", json);
    }

    [Fact]
    public void Set_WithReason_WritesObjectShape()
    {
        _marks.Set("run-1", "l1", "bad", "wrong stack");

        var json = File.ReadAllText(_ctx.MarksPath);
        Assert.Contains("\"mark\": \"bad\"", json);
        Assert.Contains("\"reason\": \"wrong stack\"", json);
    }

    [Fact]
    public void LoadAll_ReadsLegacyStringValues()
    {
        File.WriteAllText(_ctx.MarksPath, """{ "run-1": { "l1": "good", "l2": "bad" } }""");

        var run = _marks.LoadAll()["run-1"];
        Assert.Equal(new ListingMark("good", null), run["l1"]);
        Assert.Equal(new ListingMark("bad", null), run["l2"]);
    }

    [Fact]
    public void Set_NewMark_ReplacesReason()
    {
        _marks.Set("run-1", "l1", "bad", "too junior");
        _marks.Set("run-1", "l1", "good", null);

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(new ListingMark("good", null), mark);
    }

    [Fact]
    public void Set_NullMark_RemovesEntry()
    {
        _marks.Set("run-1", "l1", "bad", "wrong stack");
        _marks.Set("run-1", "l1", null, null);

        Assert.Empty(_marks.GetForRun("run-1"));
    }

    [Fact]
    public void Set_WhitespaceReason_StoresNull()
    {
        _marks.Set("run-1", "l1", "good", "   ");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Null(mark.Reason);
    }

    [Fact]
    public void Set_ReasonTooLong_Throws()
    {
        var reason = new string('x', 501);
        Assert.Throws<Jobmatch.InvalidRequestException>(() => _marks.Set("run-1", "l1", "bad", reason));
    }

    [Fact]
    public void SetStatus_RoundTrips()
    {
        _marks.Set("run-1", "l1", "good", "great fit");
        _marks.SetStatus("run-1", "l1", "applied");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(new ListingMark("good", "great fit", "applied", _clock.UtcNow), mark);
    }

    [Fact]
    public void SetStatus_WithoutMark_PersistsEntry()
    {
        _marks.SetStatus("run-1", "l1", "interview");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(new ListingMark(null, null, "interview", _clock.UtcNow), mark);
    }

    [Fact]
    public void Set_NullMark_KeepsStatus()
    {
        _marks.Set("run-1", "l1", "bad", "wrong stack");
        _marks.SetStatus("run-1", "l1", "applied");
        _marks.Set("run-1", "l1", null, null);

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(new ListingMark(null, null, "applied", _clock.UtcNow), mark);
    }

    [Fact]
    public void SetStatus_Null_KeepsMark()
    {
        _marks.Set("run-1", "l1", "good", "great fit");
        _marks.SetStatus("run-1", "l1", "applied");
        _marks.SetStatus("run-1", "l1", null);

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(new ListingMark("good", "great fit"), mark);
    }

    [Fact]
    public void ClearingBoth_RemovesEntry()
    {
        _marks.Set("run-1", "l1", "good", null);
        _marks.SetStatus("run-1", "l1", "applied");
        _marks.Set("run-1", "l1", null, null);
        _marks.SetStatus("run-1", "l1", null);

        Assert.Empty(_marks.GetForRun("run-1"));
    }

    [Fact]
    public void SetStatus_Invalid_Throws()
    {
        Assert.Throws<Jobmatch.InvalidRequestException>(() => _marks.SetStatus("run-1", "l1", "ghosted"));
    }

    [Fact]
    public void SetStatus_Uppercase_Normalises()
    {
        _marks.SetStatus("run-1", "l1", "Interview");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal("interview", mark.Status);
    }

    [Fact]
    public void SetStatus_WithoutMark_WritesStatusOnlyObjectShape()
    {
        _marks.SetStatus("run-1", "l1", "applied");

        var json = File.ReadAllText(_ctx.MarksPath);
        Assert.Contains("\"status\": \"applied\"", json);
        Assert.DoesNotContain("\"mark\"", json);
    }

    [Fact]
    public void LoadAll_ReadsStatusOnlyObject()
    {
        File.WriteAllText(_ctx.MarksPath, """{ "run-1": { "l1": { "status": "offer" } } }""");

        var run = _marks.LoadAll()["run-1"];
        Assert.Equal(new ListingMark(null, null, "offer"), run["l1"]);
    }

    [Fact]
    public void LoadAll_DropsInvalidStatus_KeepsValidMark()
    {
        File.WriteAllText(_ctx.MarksPath, """{ "run-1": { "l1": { "mark": "good", "status": "ghosted" } } }""");

        var run = _marks.LoadAll()["run-1"];
        Assert.Equal(new ListingMark("good", null), run["l1"]);
    }

    [Fact]
    public void SetStatus_ChangedStatus_Restamps()
    {
        _marks.SetStatus("run-1", "l1", "applied");
        _clock.UtcNow = _clock.UtcNow.AddDays(3);
        _marks.SetStatus("run-1", "l1", "interview");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(_clock.UtcNow, mark.StatusChangedAt);
    }

    [Fact]
    public void SetStatus_SameStatus_KeepsOriginalTimestamp()
    {
        var first = _clock.UtcNow;
        _marks.SetStatus("run-1", "l1", "applied");
        _clock.UtcNow = first.AddDays(3);
        _marks.SetStatus("run-1", "l1", "applied");

        var mark = Assert.Single(_marks.GetForRun("run-1")).Value;
        Assert.Equal(first, mark.StatusChangedAt);
    }

    [Fact]
    public void SetStatus_WritesStatusAt_AndRoundTripsThroughDisk()
    {
        _marks.SetStatus("run-1", "l1", "applied");

        Assert.Contains("\"statusAt\"", File.ReadAllText(_ctx.MarksPath));
        var reloaded = new MarksService(_ctx).GetForRun("run-1")["l1"];
        Assert.Equal(_clock.UtcNow, reloaded.StatusChangedAt);
    }

    [Fact]
    public void ClearingStatus_CollapsesBackToBareStringShape()
    {
        _marks.Set("run-1", "l1", "good", null);
        _marks.SetStatus("run-1", "l1", "applied");
        _marks.SetStatus("run-1", "l1", null);

        var json = File.ReadAllText(_ctx.MarksPath);
        Assert.Contains("\"l1\": \"good\"", json);
        Assert.DoesNotContain("statusAt", json);
    }

    [Fact]
    public void LoadAll_ObjectWithoutStatusAt_LoadsWithNullTimestamp()
    {
        File.WriteAllText(_ctx.MarksPath, """{ "run-1": { "l1": { "mark": "bad", "status": "applied" } } }""");

        var run = _marks.LoadAll()["run-1"];
        Assert.Equal(new ListingMark("bad", null, "applied"), run["l1"]);
    }

    [Fact]
    public void LoadAll_IgnoresStatusAt_WhenStatusMissing()
    {
        File.WriteAllText(_ctx.MarksPath, """{ "run-1": { "l1": { "mark": "good", "statusAt": "2026-07-01T10:00:00+00:00" } } }""");

        var run = _marks.LoadAll()["run-1"];
        Assert.Null(run["l1"].StatusChangedAt);
    }
}
