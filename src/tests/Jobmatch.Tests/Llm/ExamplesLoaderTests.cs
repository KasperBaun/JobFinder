using Jobmatch.Llm;

namespace Jobmatch.Tests.Llm;

public sealed class ExamplesLoaderTests
{
    [Fact]
    public void Parse_ValidFrontmatter_ReadsFields()
    {
        var content = """
            ---
            polarity: liked
            title: Senior .Net udvikler til afdeling i vækst
            company: Sopra Steria
            location: Copenhagen, Denmark
            seniority: senior
            primary_stack:
            - C#
            - .NET
            - Azure
            domains:
            - B2B consulting
            - Public sector
            employer_type: DK consulting
            ---

            # Why this is a good match

            Senior .NET role at a DK consultancy.
            """;

        var ex = ExamplesLoader.Parse(content);

        Assert.NotNull(ex);
        Assert.Equal("liked", ex!.Polarity);
        Assert.Equal("Sopra Steria", ex.Company);
        Assert.Equal("senior", ex.Seniority);
        Assert.Equal(["C#", ".NET", "Azure"], ex.PrimaryStack);
        Assert.Equal("DK consulting", ex.EmployerType);
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsNull()
    {
        var ex = ExamplesLoader.Parse("# Just a markdown file\n\nNo frontmatter here.");
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_DefaultsPolarityToLiked()
    {
        var content = """
            ---
            title: Some role
            company: Some Co
            ---
            """;
        var ex = ExamplesLoader.Parse(content);
        Assert.NotNull(ex);
        Assert.Equal("liked", ex!.Polarity);
    }

    [Fact]
    public void Load_NonExistentDir_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N"));
        var examples = ExamplesLoader.Load(path);
        Assert.Empty(examples);
    }

    [Fact]
    public void ToFewShotPrompt_RendersBoth_LikedAndDisliked()
    {
        var examples = new List<ExampleListing>
        {
            new("liked", "Senior .Net udvikler", "Sopra Steria", "Copenhagen", "senior", [".NET"], [], "DK consulting"),
            new("disliked", "Marketing Manager", "Some Co", "Berlin", "mid", [], [], "marketing"),
        };
        var prompt = ExamplesLoader.ToFewShotPrompt(examples);

        Assert.Contains("GOOD matches", prompt);
        Assert.Contains("Sopra Steria", prompt);
        Assert.Contains("POOR matches", prompt);
        Assert.Contains("Marketing Manager", prompt);
    }

    [Fact]
    public void ToFewShotPrompt_EmptyList_GivesPlaceholder()
    {
        var prompt = ExamplesLoader.ToFewShotPrompt([]);
        Assert.Contains("no examples supplied", prompt);
    }

    [Fact]
    public void Parse_CapturesBodyAsNote_CollapsedToOneLine()
    {
        var content = """
            ---
            polarity: disliked
            title: Some role
            company: Some Co
            ---

            # Why this is a bad match

            Wrong stack entirely.
            And too junior.
            """;

        var ex = ExamplesLoader.Parse(content);

        Assert.NotNull(ex);
        Assert.Equal("Why this is a bad match Wrong stack entirely. And too junior.", ex!.Note);
    }

    [Fact]
    public void Parse_EmptyBody_LeavesNoteNull()
    {
        var content = """
            ---
            title: Some role
            company: Some Co
            ---
            """;
        var ex = ExamplesLoader.Parse(content);
        Assert.NotNull(ex);
        Assert.Null(ex!.Note);
    }

    [Fact]
    public void Parse_LongBody_TruncatesNote()
    {
        var content = $"---\ntitle: T\ncompany: C\n---\n{new string('x', 400)}";
        var ex = ExamplesLoader.Parse(content);
        Assert.NotNull(ex);
        Assert.True(ex!.Note!.Length <= 241);
        Assert.EndsWith("…", ex.Note);
    }

    [Fact]
    public void ToFewShotPrompt_IncludesWhy_WhenNotePresent()
    {
        var examples = new List<ExampleListing>
        {
            new("disliked", "AI Engineer - Student", "Uni Co", "Copenhagen", null, [], [], null, Note: "I'm not a student"),
        };
        var prompt = ExamplesLoader.ToFewShotPrompt(examples);

        Assert.Contains("— why: I'm not a student", prompt);
    }

    [Fact]
    public void ToFewShotPrompt_NotedExamples_SurviveTheCap()
    {
        var examples = Enumerable.Range(0, 6)
            .Select(i => new ExampleListing("disliked", $"Role {i}", $"Co {i}", null, null, [], [], null))
            .Append(new ExampleListing("disliked", "Noted role", "Noted Co", null, null, [], [], null, Note: "the why"))
            .ToList();

        var prompt = ExamplesLoader.ToFewShotPrompt(examples);

        Assert.Contains("Noted role", prompt);
    }
}
