using System.Globalization;
using System.Text.Json;
using Jobmatch.Models;
using Jobmatch.Output;

namespace Jobmatch.Tests.Output;

public sealed class MarkdownReportWriterTests : IDisposable
{
    private readonly string _dir;

    public MarkdownReportWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "jobmatch-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static Match MakeMatch(double score)
    {
        var listing = new Listing(
            Id: "x",
            Portal: "test",
            Title: "Role",
            Company: "Co",
            Location: null,
            RemoteMode: RemoteMode.Unknown,
            Description: "desc",
            Url: new Uri("https://example.com/1"),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());

        var breakdown = new ScoreBreakdown(
            PrimaryStack: 0.08,
            SecondaryStack: 0.0,
            Seniority: 0.0,
            LocationRemote: 0.0,
            Domain: 0.0,
            Freshness: 0.5,
            DisqualifierPenalty: 0.0);
        var reasoning = new MatchReasoning(
            PrimaryStackHits: ["x"],
            SecondaryStackHits: [],
            DomainHits: [],
            SeniorityMatch: null,
            LocationMatch: null,
            RemoteMatch: null,
            DisqualifierHits: [],
            Notes: "n");
        return new Match(listing, score, breakdown, reasoning);
    }

    [Fact]
    public void WriteMatches_Scores_Use_InvariantCulture_Not_CurrentCulture()
    {
        // Danish culture writes "0,35" for a double; markdown output must stay "0.35".
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");
            var path = Path.Combine(_dir, "top_jobs.md");
            MarkdownReportWriter.WriteMatches([MakeMatch(0.35)], path);

            var content = File.ReadAllText(path);

            Assert.Contains("score 0.35", content);
            Assert.Contains("| primary_stack | 0.08 |", content);
            Assert.DoesNotContain("0,35", content);
            Assert.DoesNotContain("0,08", content);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void WriteMatches_Title_Is_Not_SpectreMarkupEscaped()
    {
        var path = Path.Combine(_dir, "top_jobs.md");
        MarkdownReportWriter.WriteMatches([MakeMatch(0.5)], path, title: "Top matches for [Test] User");

        var content = File.ReadAllText(path);

        // The title must appear literally; Spectre markup escaping ("[[" / "]]") would corrupt markdown.
        Assert.Contains("# Top matches for [Test] User", content);
        Assert.DoesNotContain("[[Test]]", content);
    }

    [Fact]
    public void WriteMatches_Breakdown_Rows_Are_Sorted_By_Score_Desc()
    {
        var path = Path.Combine(_dir, "top_jobs.md");
        MarkdownReportWriter.WriteMatches([MakeMatch(0.5)], path);

        var content = File.ReadAllText(path);

        // freshness (0.5) comes before primary_stack (0.08) in the rendered breakdown table.
        var freshnessIdx = content.IndexOf("| freshness", StringComparison.Ordinal);
        var primaryIdx = content.IndexOf("| primary_stack", StringComparison.Ordinal);
        Assert.True(freshnessIdx >= 0 && primaryIdx >= 0);
        Assert.True(freshnessIdx < primaryIdx, "breakdown rows must be sorted by score descending");
    }

    [Fact]
    public void WriteMatches_Url_Uses_Autolink_Syntax()
    {
        var path = Path.Combine(_dir, "top_jobs.md");
        MarkdownReportWriter.WriteMatches([MakeMatch(0.5)], path);

        var content = File.ReadAllText(path);
        // <url> autolink survives URLs containing ')' which would otherwise break [text](url) syntax.
        Assert.Contains("<https://example.com/1>", content);
    }

    [Fact]
    public void WriteMatches_Empty_List_Writes_Placeholder()
    {
        var path = Path.Combine(_dir, "top_jobs.md");
        MarkdownReportWriter.WriteMatches([], path);

        var content = File.ReadAllText(path);
        Assert.Contains("No matches above the minimum score threshold.", content);
    }
}
