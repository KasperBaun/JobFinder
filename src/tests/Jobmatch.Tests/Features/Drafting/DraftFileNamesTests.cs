using Jobmatch.Features.Drafting;

namespace Jobmatch.Tests.Features.Drafting;

public sealed class DraftFileNamesTests
{
    [Fact]
    public void Resume_And_CoverLetter_ShareAStemAndDifferBySuffix()
    {
        var draft = Draft("Backend Developer", "Acme A/S");

        Assert.Equal("Acme_A_S_Backend_Developer_abcdef12_Resume.docx", DraftFileNames.Resume(draft, "abcdef1234567890"));
        Assert.Equal("Acme_A_S_Backend_Developer_abcdef12_CoverLetter.docx", DraftFileNames.CoverLetter(draft, "abcdef1234567890"));
    }

    // Company and title come off a job ad, so nothing there may steer the write out of documents/.
    [Theory]
    [InlineData("../../etc")]
    [InlineData(@"a/b\c")]
    [InlineData("Acme: The <Best>")]
    public void Stem_StripsPathAndQuotingCharacters(string company)
    {
        var stem = DraftFileNames.Stem(Draft("Role", company), "id123456");

        Assert.DoesNotContain('/', stem);
        Assert.DoesNotContain('\\', stem);
        Assert.DoesNotContain("..", stem);
        Assert.Equal(stem, Path.GetFileName(stem));
    }

    [Fact]
    public void Stem_BlankFields_FallBackToPlaceholders()
    {
        var stem = DraftFileNames.Stem(Draft("", "   "), "id123456");

        Assert.StartsWith("Company_Role_", stem);
    }

    [Fact]
    public void Stem_NonLatinTitle_StillYieldsAUsableName()
    {
        var stem = DraftFileNames.Stem(Draft("Softwareudvikler", "Ørsted"), "id123456");

        Assert.Contains("rsted", stem);
        Assert.Contains("Softwareudvikler", stem);
    }

    [Fact]
    public void Stem_VeryLongFields_AreBounded()
    {
        var stem = DraftFileNames.Stem(Draft(new string('r', 200), new string('c', 200)), "id123456");

        Assert.True(stem.Length < 160, $"stem was {stem.Length} chars");
    }

    [Fact]
    public void Stem_SameRoleAtSameCompany_DiffersByListingId()
    {
        var draft = Draft("Developer", "Acme");

        Assert.NotEqual(DraftFileNames.Stem(draft, "aaaaaaaa11"), DraftFileNames.Stem(draft, "bbbbbbbb22"));
    }

    private static ApplicationDraft Draft(string title, string company) =>
        new(title, company, "resume", "cover letter");
}
