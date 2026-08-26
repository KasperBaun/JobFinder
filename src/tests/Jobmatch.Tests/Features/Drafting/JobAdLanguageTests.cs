using Jobmatch.Features.Drafting;

namespace Jobmatch.Tests.Features.Drafting;

/// <summary>
/// Asking the model to write "in the same language as the job ad" produced English letters for every
/// Danish ad in a real run — a Danish ad carries an English company boilerplate, an English ad carries
/// a Danish job title, and a small model follows whichever it saw most of. So the language is decided
/// here and stated in the prompt instead.
/// </summary>
public sealed class JobAdLanguageTests
{
    private const string DanishAd =
        "Vi søger en erfaren .NET-udvikler til vores team i København. Du kommer til at arbejde med " +
        "microservices og API'er, og du får mulighed for at være med til at forme arkitekturen. " +
        "Vi tilbyder en hverdag med stor frihed under ansvar, og vi lægger vægt på, at du har lyst " +
        "til at lære nyt. Du skal have erfaring med C# og Azure, og det er en fordel hvis du " +
        "kender til React.";

    private const string EnglishAd =
        "We are looking for an experienced .NET developer to join our team in Copenhagen. You will " +
        "work with microservices and APIs, and you will have the opportunity to help shape the " +
        "architecture. We offer a great deal of freedom, and we value a willingness to learn. You " +
        "should have experience with C# and Azure, and knowledge of React is an advantage.";

    [Fact]
    public void DanishAd_IsDanish() => Assert.Equal(DraftLanguage.Danish, JobAdLanguage.Of(DanishAd));

    [Fact]
    public void EnglishAd_IsEnglish() => Assert.Equal(DraftLanguage.English, JobAdLanguage.Of(EnglishAd));

    // A Danish employer, address and job title in an otherwise English ad is still an English ad —
    // which is why this counts function words rather than looking for Danish-looking spelling.
    [Fact]
    public void EnglishAdFromADanishEmployer_IsStillEnglish()
    {
        var ad = "Jyske Bank, Silkeborg — Softwareudvikler .NET/Cloud. " + EnglishAd;

        Assert.Equal(DraftLanguage.English, JobAdLanguage.Of(ad));
    }

    // The worst-polluted ad in the sample run: a Danish posting wrapped in English portal chrome.
    [Fact]
    public void DanishAdDilutedByEnglishPortalChrome_IsStillDanish()
    {
        var ad = "Sopra Steria | SmartRecruiters. Sign in. Create account. Cookie settings. "
            + "Privacy policy. Terms of use. Browse jobs. " + DanishAd;

        Assert.Equal(DraftLanguage.Danish, JobAdLanguage.Of(ad));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345 -- ...")]
    public void NothingToGoOn_DefaultsToEnglish(string? ad) =>
        Assert.Equal(DraftLanguage.English, JobAdLanguage.Of(ad));
}
