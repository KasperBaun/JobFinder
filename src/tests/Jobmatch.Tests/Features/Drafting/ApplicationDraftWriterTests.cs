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
        var prompt = ApplicationDraftWriter.BuildSystemPrompt(DraftLanguage.English);

        Assert.Contains("NEVER invent", prompt);
        Assert.Contains("resumeMarkdown", prompt);
        Assert.Contains("coverLetterMarkdown", prompt);
    }

    // The ban used to sit only in the preamble and in the resume's own rule, and the model obeyed it
    // where it was restated: resumes stayed thin on a weak match while the letter claimed the ad's
    // requirements back as experience. The letter is the document a human reads first.
    [Fact]
    public void BuildSystemPrompt_ExtendsTheInventionBanToTheCoverLetter()
    {
        var prompt = ApplicationDraftWriter.BuildSystemPrompt(DraftLanguage.English);

        Assert.Contains("applies to the cover letter", prompt);
        Assert.Contains("write LESS rather than filling the space", prompt);
        // The abstract ban alone still let a weak match claim "my experience with Azure Data Factory",
        // a tool the CV never names. A rule about names is one the model can actually check itself against.
        Assert.Contains("NEVER name a technology, tool, platform or methodology", prompt);
        Assert.Contains("unless that exact name appears in the CV", prompt);
    }

    [Theory]
    [InlineData(DraftLanguage.Danish, "Danish")]
    [InlineData(DraftLanguage.English, "English")]
    public void BuildSystemPrompt_StatesTheLanguageRatherThanLeavingItToBeInferred(
        DraftLanguage language, string expected)
    {
        var prompt = ApplicationDraftWriter.BuildSystemPrompt(language);

        Assert.Contains($"Write both documents in {expected}", prompt);
        Assert.Contains("including the section headings and the closing", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_BansThePlaceholderAndTheClicheOpening()
    {
        var prompt = ApplicationDraftWriter.BuildSystemPrompt(DraftLanguage.English);

        Assert.Contains("NEVER write a placeholder in square or angle brackets", prompt);
        Assert.Contains("no letterhead, no address block, no date line", prompt);
        Assert.Contains("I am writing to express my interest", prompt);
    }

    // Four of six letters came back with no sign-off at all when this was one trailing rule among
    // several. It is content the letter is missing, so it is spelled out as a step, with the closing
    // the language actually uses.
    [Theory]
    [InlineData(DraftLanguage.English, "Sincerely")]
    [InlineData(DraftLanguage.Danish, "Med venlig hilsen")]
    public void BuildSystemPrompt_SpellsOutTheClosing(DraftLanguage language, string closing)
    {
        var prompt = ApplicationDraftWriter.BuildSystemPrompt(language);

        Assert.Contains($"The closing line \"{closing},\"", prompt);
        Assert.Contains("a letter without it is incomplete", prompt);
    }

    // Spelling the closing out as a literal template made the model copy the template: one letter
    // shipped ending "<the candidate's name>". The prompt describes the shape now, and this is the
    // net under it — a line that is only a bracket placeholder is never something to send.
    [Theory]
    [InlineData("Dear team,\n\nBody.\n\nSincerely,\n<the candidate's name>", "Sincerely,")]
    [InlineData("[Recruiting Team]\n[Date]\n\nDear team,\n\nBody.", "Dear team,")]
    public void ParseDraft_DropsPlaceholderLines(string letter, string expectedTail)
    {
        var json = $$"""{"resumeMarkdown":"r","coverLetterMarkdown":{{System.Text.Json.JsonSerializer.Serialize(letter)}}}""";

        var draft = ApplicationDraftWriter.ParseDraft(json, "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.DoesNotContain("<the candidate's name>", draft!.CoverLetterMarkdown);
        Assert.DoesNotContain("[Date]", draft.CoverLetterMarkdown);
        Assert.Contains(expectedTail, draft.CoverLetterMarkdown);
    }

    // The marker that tells the model an over-long ad was cut is our note to it, not prose — one
    // letter copied it into the sign-off.
    [Fact]
    public void ParseDraft_StripsOurOwnTruncationMarker()
    {
        var draft = ApplicationDraftWriter.ParseDraft(
            $$"""{"resumeMarkdown":"## Skills {{ApplicationDraftWriter.TruncationMarker}}","coverLetterMarkdown":"Body. {{ApplicationDraftWriter.TruncationMarker}} Sincerely,\nJane"}""",
            "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.DoesNotContain(ApplicationDraftWriter.TruncationMarker, draft!.CoverLetterMarkdown);
        Assert.DoesNotContain(ApplicationDraftWriter.TruncationMarker, draft.ResumeMarkdown);
    }

    [Fact]
    public void ParseDraft_KeepsRealProseContainingBrackets()
    {
        var draft = ApplicationDraftWriter.ParseDraft(
            """{"resumeMarkdown":"r","coverLetterMarkdown":"I worked on [the] platform team.\n\nSincerely,\nJane"}""",
            "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.Contains("[the] platform team", draft!.CoverLetterMarkdown);
    }

    [Fact]
    public void BuildUserPrompt_LabelsCvAsTheOnlyFactSource()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(
            new DraftInputs("Jane Doe, 7 years C#", null, "Backend Developer", "Acme A/S", "We are hiring a Backend Developer"));

        Assert.Contains("the only source of facts", prompt);
        Assert.Contains("Jane Doe, 7 years C#", prompt);
        Assert.Contains("We are hiring a Backend Developer", prompt);
    }

    [Fact]
    public void BuildUserPrompt_StatesTheRoleAsAuthoritative()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(
            new DraftInputs("cv", null, "Senior Software Engineer C#/.net", "Danske Bank", "ad"));

        Assert.Contains("authoritative", prompt);
        Assert.Contains("Title: Senior Software Engineer C#/.net", prompt);
        Assert.Contains("Company: Danske Bank", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ListingWithoutACompany_OmitsTheLine()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(
            new DraftInputs("cv", null, "Backend Developer", null, "ad"));

        Assert.DoesNotContain("Company:", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithoutSkillset_OmitsTargetingSection()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", null, "Backend Developer", "Acme A/S", "ad"));

        Assert.DoesNotContain("CANDIDATE TARGETING", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithSkillset_MarksItAsAimNotFact()
    {
        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", Skillset(), "Backend Developer", "Acme A/S", "ad"));

        Assert.Contains("CANDIDATE TARGETING", prompt);
        Assert.Contains("not facts to assert", prompt);
        Assert.Contains("C#", prompt);
    }

    [Fact]
    public void BuildUserPrompt_LongAd_IsTruncated()
    {
        var ad = new string('x', ApplicationDraftWriter.MaxAdChars + 500);

        var prompt = ApplicationDraftWriter.BuildUserPrompt(new DraftInputs("cv", null, "Backend Developer", "Acme A/S", ad));

        Assert.Contains("truncated", prompt);
        Assert.True(prompt.Length < ad.Length + 2000);
    }

    [Fact]
    public void ParseDraft_StrictJson_ReadsAllFields()
    {
        var draft = ApplicationDraftWriter.ParseDraft(FullJson, "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.Equal("Backend Developer", draft!.JobTitle);
        Assert.Equal("Acme A/S", draft.CompanyName);
        Assert.Contains("Built things", draft.ResumeMarkdown);
        Assert.Contains("Dear hiring team", draft.CoverLetterMarkdown);
    }

    [Fact]
    public void ParseDraft_CodeFencedJson_Parses()
    {
        var draft = ApplicationDraftWriter.ParseDraft("```json\n" + FullJson + "\n```", "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.Equal("Acme A/S", draft!.CompanyName);
    }

    [Fact]
    public void ParseDraft_JsonWrappedInProse_Parses()
    {
        var draft = ApplicationDraftWriter.ParseDraft("Sure! Here you go:\n" + FullJson + "\nGood luck!", "Backend Developer", "Acme A/S");

        Assert.NotNull(draft);
        Assert.Equal("Backend Developer", draft!.JobTitle);
    }

    // The listing record knows what the role is. A model reading it back out of scraped ad text gets
    // it wrong — and the filenames are built from it, so a wrong title is a file the user cannot find.
    [Fact]
    public void ParseDraft_TakesRoleFromTheListing_NotFromTheReply()
    {
        var draft = ApplicationDraftWriter.ParseDraft(
            """{"jobTitle":"Senior Developer","companyName":"Guessed ApS","resumeMarkdown":"r","coverLetterMarkdown":"c"}""",
            "Senior Software Engineer C#/.net",
            "Danske Bank");

        Assert.NotNull(draft);
        Assert.Equal("Senior Software Engineer C#/.net", draft!.JobTitle);
        Assert.Equal("Danske Bank", draft.CompanyName);
    }

    [Fact]
    public void ParseDraft_ListingWithoutACompany_LeavesItEmpty()
    {
        var draft = ApplicationDraftWriter.ParseDraft(
            """{"resumeMarkdown":"r","coverLetterMarkdown":"c"}""", "Backend Developer", null);

        Assert.NotNull(draft);
        Assert.Equal(string.Empty, draft!.CompanyName);
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
        Assert.Null(ApplicationDraftWriter.ParseDraft(raw, "Backend Developer", "Acme A/S"));
    }

    [Fact]
    public async Task WriteAsync_RetriesOnce_ThenSucceeds()
    {
        var client = new FakeLlmClient("truncated {", FullJson);
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        var draft = await writer.WriteAsync(new DraftInputs("cv", null, "Backend Developer", "Acme A/S", "ad"));

        Assert.Equal("Acme A/S", draft.CompanyName);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task WriteAsync_FailsAfterRetry_ThrowsInvalidRequest()
    {
        var client = new FakeLlmClient("garbage", "still garbage");
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => writer.WriteAsync(new DraftInputs("cv", null, "Backend Developer", "Acme A/S", "ad")));
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task WriteAsync_UnreachableModel_ThrowsWithoutCalling()
    {
        var client = new FakeLlmClient { Reachable = false };
        var writer = new ApplicationDraftWriter(client, NullLogger<ApplicationDraftWriter>.Instance);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => writer.WriteAsync(new DraftInputs("cv", null, "Backend Developer", "Acme A/S", "ad")));
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
