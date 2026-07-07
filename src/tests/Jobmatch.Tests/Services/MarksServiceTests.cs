using Jobmatch.Services;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Services;

public sealed class MarksServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly MarksService _marks;

    public MarksServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-marks-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "marks@example.com", repoRoot: _tempRoot, seedExamples: false);
        _marks = new MarksService(_ctx);
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
}
