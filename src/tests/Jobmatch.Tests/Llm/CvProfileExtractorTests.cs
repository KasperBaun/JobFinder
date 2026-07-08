using Jobmatch.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Llm;

public sealed class CvProfileExtractorTests
{
    private const string FullJson = """
        {"name":"Jane Doe","location":"Copenhagen","country":"Denmark","region":"Hovedstaden",
         "metro":["Copenhagen","Lyngby"],"experienceYears":7,"seniority":"senior","remotePreference":"hybrid",
         "targetRoles":["Backend Developer"],"primaryStack":["C#",".NET","SQL"],"secondaryStack":["Docker"],
         "domains":["fintech"],"languages":["Danish","English"],"employmentTypes":["full-time"]}
        """;

    [Fact]
    public void BuildSystemPrompt_Describes_Schema_And_Rules()
    {
        var prompt = CvProfileExtractor.BuildSystemPrompt();

        Assert.Contains("primaryStack", prompt);
        Assert.Contains("experienceYears", prompt);
        Assert.Contains("null", prompt);
        Assert.Contains("NOT programming languages", prompt);
    }

    [Fact]
    public void ParseProfile_StrictJson_ReadsAllFields()
    {
        var p = CvProfileExtractor.ParseProfile(FullJson);

        Assert.NotNull(p);
        Assert.Equal("Jane Doe", p!.Name);
        Assert.Equal("Copenhagen", p.Location);
        Assert.Equal("Denmark", p.Country);
        Assert.Equal(7, p.ExperienceYears);
        Assert.Equal("senior", p.Seniority);
        Assert.Equal("hybrid", p.RemotePreference);
        Assert.Equal(["C#", ".NET", "SQL"], p.PrimaryStack);
        Assert.Equal(["Danish", "English"], p.Languages);
    }

    [Fact]
    public void ParseProfile_CodeFencedJson_Parses()
    {
        var p = CvProfileExtractor.ParseProfile("```json\n" + FullJson + "\n```");

        Assert.NotNull(p);
        Assert.Equal("Jane Doe", p!.Name);
    }

    [Fact]
    public void ParseProfile_JsonWrappedInProse_Parses()
    {
        var p = CvProfileExtractor.ParseProfile("Here is the profile:\n" + FullJson + "\nLet me know!");

        Assert.NotNull(p);
        Assert.Equal("Copenhagen", p!.Location);
    }

    [Fact]
    public void ParseProfile_InvalidEnumValues_BecomeNull()
    {
        var p = CvProfileExtractor.ParseProfile(
            """{"name":"X","seniority":"guru","remotePreference":"office-only"}""");

        Assert.NotNull(p);
        Assert.Null(p!.Seniority);
        Assert.Null(p.RemotePreference);
    }

    [Theory]
    [InlineData("-3")]
    [InlineData("99")]
    [InlineData("\"seven\"")]
    public void ParseProfile_ImplausibleYears_BecomeNull(string years)
    {
        var p = CvProfileExtractor.ParseProfile($$"""{"name":"X","experienceYears":{{years}}}""");

        Assert.NotNull(p);
        Assert.Null(p!.ExperienceYears);
    }

    [Fact]
    public void ParseProfile_MissingFields_DefaultToNullsAndEmptyLists()
    {
        var p = CvProfileExtractor.ParseProfile("""{"name":"X"}""");

        Assert.NotNull(p);
        Assert.Null(p!.Location);
        Assert.Null(p.ExperienceYears);
        Assert.Empty(p.PrimaryStack);
        Assert.Empty(p.TargetRoles);
    }

    [Fact]
    public void ParseProfile_SingleStringInsteadOfArray_BecomesOneItemList()
    {
        var p = CvProfileExtractor.ParseProfile("""{"primaryStack":"C#"}""");

        Assert.NotNull(p);
        Assert.Equal(["C#"], p!.PrimaryStack);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no json here")]
    [InlineData("[1,2,3]")]
    public void ParseProfile_Unparseable_ReturnsNull(string raw)
    {
        Assert.Null(CvProfileExtractor.ParseProfile(raw));
    }

    [Fact]
    public async Task ExtractAsync_RetriesOnce_ThenSucceeds()
    {
        var client = new FakeLlmClient("not json at all", FullJson);
        var extractor = new CvProfileExtractor(client, NullLogger<CvProfileExtractor>.Instance);

        var p = await extractor.ExtractAsync("some cv text");

        Assert.Equal("Jane Doe", p.Name);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task ExtractAsync_FailsAfterRetry_ThrowsInvalidRequest()
    {
        var client = new FakeLlmClient("garbage", "still garbage");
        var extractor = new CvProfileExtractor(client, NullLogger<CvProfileExtractor>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(() => extractor.ExtractAsync("cv"));
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task ExtractAsync_UnreachableModel_ThrowsInvalidRequest()
    {
        var client = new FakeLlmClient { Reachable = false };
        var extractor = new CvProfileExtractor(client, NullLogger<CvProfileExtractor>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(() => extractor.ExtractAsync("cv"));
        Assert.Equal(0, client.Calls);
    }

    private sealed class FakeLlmClient(params string[] responses) : ILlmClient
    {
        public bool Reachable { get; init; } = true;
        public int Calls { get; private set; }

        public Task<bool> IsReachableAsync(CancellationToken ct = default) => Task.FromResult(Reachable);

        public Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            var response = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(response);
        }
    }
}
