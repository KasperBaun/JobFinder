using Jobmatch.Domain;
using Jobmatch.Features.Drafting;
using Jobmatch.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Features.Drafting;

public sealed class ApplicationDraftWriterTests
{
    private const string FullJson = """
        {"jobTitle":"Backend Developer","companyName":"Acme A/S",
         "resumeMarkdown":"## Experience\n- Built things","coverLetterMarkdown":"Dear hiring team,\n\nI am writing…"}
        """;

    [Fact]
    public void BuildSystemPrompt_ForbidsInvention_AndDeclaresSchema()
    {
        var prompt = ApplicationDraftWriter.BuildSystemPrompt();

        Assert.Contains("NEVER invent", prompt);
        Assert.Contains("resumeMarkdown", prompt);
        Assert.Contains("coverLetterMarkdown", prompt);
    }

    [Fact]
    public void BuildUserPrompt_LabelsCvAsTheOnlyFactSource()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(
            new DraftInputs("Jane Doe, 7 years C#", null, "We are hiring a Backend Developer"));

        Assert.Contains("the only source of facts", prompt);
        Assert.Contains("Jane Doe, 7 years C#", prompt);
        Assert.Contains("We are hiring a Backend Developer", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithoutSkillset_OmitsTargetingSection()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", null, "ad"));

        Assert.DoesNotContain("CANDIDATE TARGETING", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithSkillset_MarksItAsAimNotFact()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", Skillset(), "ad"));

        Assert.Contains("CANDIDATE TARGETING", prompt);
        Assert.Contains("not facts to assert", prompt);
        Assert.Contains("C#", prompt);
    }

    [Fact]
    public void BuildUserPrompt_LongAd_IsTruncated()
    {
        var ad = new string('x', ApplicationDraftWriter.MaxAdChars + 500);

        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", null, ad));

        Assert.Contains("truncated", prompt);
        Assert.True(prompt.Length < ad.Length + 2000);
    }

    [Fact]
    public void ParseDraft_StrictJson_ReadsAllFields()
    {
        var draft = ApplicationDraftWriter.ParseDraft(FullJson);

        Assert.NotNull(draft);
        Assert.Equal("Backend Developer", draft!.JobTitle);
        Assert.Equal("Acme A/S", draft.CompanyName);
        Assert.Contains("Built things", draft.ResumeMarkdown);
        Assert.Contains("Dear hiring team", draft.CoverLetterMarkdown);
    }

    [Fact]
    public void ParseDraft_CodeFencedJson_Parses()
    {
        var draft = ApplicationDraftWriter.ParseDraft("```json\n" + FullJson + "\n```");

        Assert.NotNull(draft);
        Assert.Equal("Acme A/S", draft!.CompanyName);
    }

    [Fact]
    public void ParseDraft_JsonWrappedInProse_Parses()
    {
        var draft = ApplicationDraftWriter.ParseDraft("Sure! Here you go:\n" + FullJson + "\nGood luck!");

        Assert.NotNull(draft);
        Assert.Equal("Backend Developer", draft!.JobTitle);
    }

    [Fact]
    public void ParseDraft_MissingTitleAndCompany_DefaultToEmpty()
    {
        var draft = ApplicationDraftWriter.ParseDraft(
            """{"resumeMarkdown":"r","coverLetterMarkdown":"c"}""");

        Assert.NotNull(draft);
        Assert.Equal(string.Empty, draft!.JobTitle);
        Assert.Equal(string.Empty, draft.CompanyName);
    }

    // A draft missing either document is a failed run, not a partial one — unlike CV extraction,
    // where an absent field is simply unknown.
    [Theory]
    [InlineData("""{"resumeMarkdown":"r"}""")]
    [InlineData("""{"coverLetterMarkdown":"c"}""")]
    [InlineData("""{"resumeMarkdown":"","coverLetterMarkdown":"c"}""")]
    [InlineData("")]
    [InlineData("no json here")]
    [InlineData("[1,2,3]")]
    public void ParseDraft_MissingEitherDocument_ReturnsNull(string raw)
    {
        Assert.Null(ApplicationDraftWriter.ParseDraft(raw));
    }

    [Fact]
    public async Task WriteAsync_RetriesOnce_ThenSucceeds()
    {
        var client = new FakeLlmClient("truncated {", FullJson);
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        var draft = await writer.WriteAsync(new DraftInputs("cv", null, "ad"));

        Assert.Equal("Acme A/S", draft.CompanyName);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task WriteAsync_FailsAfterRetry_ThrowsInvalidRequest()
    {
        var client = new FakeLlmClient("garbage", "still garbage");
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => writer.WriteAsync(new DraftInputs("cv", null, "ad")));
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task WriteAsync_UnreachableModel_ThrowsWithoutCalling()
    {
        var client = new FakeLlmClient { Reachable = false };
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => writer.WriteAsync(new DraftInputs("cv", null, "ad")));
        Assert.Equal(0, client.Calls);
    }

    private static Skillset Skillset() => new(
        Name: "Jane Doe",
        Location: "Copenhagen",
        ExperienceYears: 7,
        TargetRoles: ["Backend Developer"],
        RemotePreference: RemotePreference.Hybrid,
        Seniority: Seniority.Senior,
        PrimaryStack: ["C#", ".NET"],
        SecondaryStack: ["Docker"],
        Domains: ["fintech"],
        Disqualifiers: [],
        Languages: ["Danish", "English"],
        EmploymentTypes: ["full-time"]);

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
