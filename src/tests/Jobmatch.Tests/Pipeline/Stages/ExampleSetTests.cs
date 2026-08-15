using System.Text.Json;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Applications;
using Jobmatch.Search.Judging;
using Jobmatch.Infrastructure.Json;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Pipeline.Stages;

public sealed class ExampleSetTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;
    private readonly ExampleSet _examples;
    private readonly MarksService _marks;

    public ExampleSetTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-search-examples-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(emailOverride: "examples@example.com", repoRoot: _tempRoot, seedExamples: false);
        _marks = new MarksService(_ctx);
        _examples = new ExampleSet(_ctx, TestServices.Runs(_ctx), _marks);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private void WriteCuratedExample(string fileName, string title, string company)
    {
        Directory.CreateDirectory(_ctx.ExamplesDir);
        File.WriteAllText(Path.Combine(_ctx.ExamplesDir, fileName), $"""
            ---
            polarity: disliked
            title: {title}
            company: {company}
            ---
            Curated why.
            """);
    }

    private void WriteMarkedHistory(string runId, string listingId, string title, string company, string mark, string? reason)
    {
        var detail = new RunDetail(
            RunId: runId,
            StartedAt: DateTimeOffset.UtcNow,
            Providers: [],
            FetchedCount: 0,
            DedupedCount: 0,
            RankedCount: 0,
            ShortlistCount: 1,
            TopScore: 0.8,
            GoodMarks: 0,
            Shortlist: [new ListingMatch(listingId, "p", title, company, null, "onsite", "https://x/1", null, 0.8, "", [], [])],
            Marks: new Dictionary<string, string>());
        File.WriteAllText(
            Path.Combine(_ctx.HistoryDir, $"{runId}.json"),
            JsonSerializer.Serialize(detail, JobmatchJsonOptions.Indented));
        _marks.Set(runId, listingId, mark, reason);
    }

    [Fact]
    public void Load_MergesCuratedAndMarked()
    {
        WriteCuratedExample("disliked-junior.md", "Junior Developer", "Junior Co");
        WriteMarkedHistory("20260701-100000-aaaaaa", "l1", "AI Engineer - Student", "Uni Co", "bad", "I'm not a student");

        var examples = _examples.Load();

        Assert.Equal(2, examples.Count);
        Assert.Contains(examples, e => e.Company == "Junior Co" && e.Note == "Curated why.");
        Assert.Contains(examples, e => e.Company == "Uni Co" && e.Note == "I'm not a student");
    }

    [Fact]
    public void Load_CuratedWins_OnTitleCompanyCollision()
    {
        WriteCuratedExample("disliked-student.md", "AI Engineer - Student", "Uni Co");
        WriteMarkedHistory("20260701-100000-aaaaaa", "l1", "AI Engineer - Student", "Uni Co", "bad", "marked why");

        var example = Assert.Single(_examples.Load());
        Assert.Equal("Curated why.", example.Note);
    }

    [Fact]
    public void Load_NoMarks_ReturnsCuratedOnly()
    {
        WriteCuratedExample("disliked-junior.md", "Junior Developer", "Junior Co");

        var example = Assert.Single(_examples.Load());
        Assert.Equal("Junior Co", example.Company);
    }
}
