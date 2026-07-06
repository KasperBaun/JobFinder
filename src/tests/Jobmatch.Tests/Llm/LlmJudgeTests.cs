using Jobmatch.Llm;
using Jobmatch.Models;

namespace Jobmatch.Tests.Llm;

public sealed class LlmJudgeTests
{
    [Fact]
    public void BuildSystemPrompt_Includes_Preferred_Companies_When_Present()
    {
        var skillset = new Skillset(
            Name: "A",
            Location: "Copenhagen",
            ExperienceYears: 5,
            TargetRoles: ["Software Engineer"],
            RemotePreference: RemotePreference.Hybrid,
            Seniority: Seniority.Mid,
            PrimaryStack: ["C#"],
            SecondaryStack: [],
            Domains: [],
            Disqualifiers: [],
            Languages: ["English"],
            EmploymentTypes: ["full-time"])
        {
            PreferredCompanies = ["LEGO", "Maersk"],
        };

        var prompt = LlmJudge.BuildSystemPrompt(skillset, []);

        Assert.Contains("LEGO, Maersk", prompt);
    }

    [Fact]
    public void ParseVerdict_StrictJson_Parses()
    {
        var v = LlmJudge.ParseVerdict("""{"score": 0.8, "reason": "good fit"}""");
        Assert.NotNull(v);
        Assert.Equal(0.8, v!.Score, precision: 4);
        Assert.Equal("good fit", v.Reason);
    }

    [Fact]
    public void ParseVerdict_CodeFencedJson_Parses()
    {
        var v = LlmJudge.ParseVerdict("""
            ```json
            {"score": 0.42, "reason": "wrong stack"}
            ```
            """);
        Assert.NotNull(v);
        Assert.Equal(0.42, v!.Score, precision: 4);
        Assert.Equal("wrong stack", v.Reason);
    }

    [Fact]
    public void ParseVerdict_ScoreOutOfRange_Clamped()
    {
        var v = LlmJudge.ParseVerdict("""{"score": 1.7, "reason": "x"}""");
        Assert.NotNull(v);
        Assert.Equal(1.0, v!.Score);
    }

    [Fact]
    public void ParseVerdict_LooseJsonInPaddedText_Salvaged()
    {
        // Some models prepend prose before the JSON object.
        var v = LlmJudge.ParseVerdict("Here is my judgment: {\"score\": 0.65, \"reason\": \"close\"} done.");
        Assert.NotNull(v);
        Assert.Equal(0.65, v!.Score, precision: 4);
    }

    [Fact]
    public void ParseVerdict_GarbageReturnsNull()
    {
        Assert.Null(LlmJudge.ParseVerdict("totally not json"));
        Assert.Null(LlmJudge.ParseVerdict(""));
        Assert.Null(LlmJudge.ParseVerdict(null!));
    }
}
