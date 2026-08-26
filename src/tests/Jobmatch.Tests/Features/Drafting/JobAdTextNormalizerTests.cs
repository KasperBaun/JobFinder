using Jobmatch.Features.Drafting;

namespace Jobmatch.Tests.Features.Drafting;

/// <summary>
/// The samples here are shortened from what the shipped portals actually recorded in a run: an Oracle
/// careers page whose whole "ad text" is stylesheet, a jobindex teaser that stops after the first
/// sentence, and a Workday ad that arrives clean. A description being present is not the same as it
/// being an ad, and the first two used to reach the model as if they were.
/// </summary>
public sealed class JobAdTextNormalizerTests
{
    private const string RealAd =
        "Join some of the most innovative thinkers in FinTech as we lead the evolution of financial " +
        "technology. You will design and build services in C# and .NET, working closely with product " +
        "owners to turn requirements into software our clients depend on every day. We are looking " +
        "for someone who enjoys owning a feature end to end, from the database through to the user " +
        "interface, and who cares about the quality of what they ship. You will join a team that " +
        "values pragmatism over ceremony and that reviews every change together before it goes out.";

    private const string StylesheetOnly =
        "Danske Bank const HASHBANG_REGEX = /\\/?#\\/(job|requisitions|jobs)\\//; " +
        "if (window.location.href.match(HASHBANG_REGEX)) { window.location.replace(1); } " +
        "/**element.style { } [dir] .talent-community-tile--tile { background:#505050; " +
        "border-radius: 4px; padding: 25px 20px; margin-bottom: 20px; } [dir] .app-footer " +
        "{ background: hsl(201deg 100% 16%); } **/";

    [Fact]
    public void CleanAd_SurvivesUntouched()
    {
        var normalized = JobAdTextNormalizer.Normalize(RealAd);

        Assert.Equal(RealAd, normalized);
    }

    [Fact]
    public void PageFurnitureOnly_IsRefused()
    {
        Assert.Equal(string.Empty, JobAdTextNormalizer.Normalize(StylesheetOnly));
    }

    [Fact]
    public void AdWrappedInPageFurniture_KeepsTheAdAndDropsTheRest()
    {
        var normalized = JobAdTextNormalizer.Normalize(StylesheetOnly + " " + RealAd + " " + StylesheetOnly);

        Assert.Contains("Join some of the most innovative thinkers", normalized);
        Assert.Contains("reviews every change together", normalized);
        Assert.DoesNotContain("border-radius", normalized);
        Assert.DoesNotContain("HASHBANG_REGEX", normalized);
    }

    // The same ad reaches jobindex as two sentences of marketing copy followed by Workday's loader.
    // There is nothing there to tailor against, and the full version is in the run under its own portal.
    [Fact]
    public void SyndicatedTeaser_IsRefused()
    {
        const string teaser =
            "Senior Full-Stack Software Engineer (.Net/Angular) København WHAT MAKES US, US Join some " +
            "of the most innovative thinkers in FinTech as we lead the evolution of financial technology. " +
            "window.workday = window.workday || {}; if (typeof Symbol === 'undefined') { " +
            "createScriptTag(sharedVendorsLoaderUrlOrigin + sharedVendorLoaderAsset); }";

        Assert.Equal(string.Empty, JobAdTextNormalizer.Normalize(teaser));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Finlandsgade 10, 8200 Aarhus N")]
    public void NothingToTailorAgainst_IsRefused(string? raw)
    {
        Assert.Equal(string.Empty, JobAdTextNormalizer.Normalize(raw));
    }

    [Fact]
    public void UnmatchedBrace_DoesNotSwallowTheAd()
    {
        var normalized = JobAdTextNormalizer.Normalize("We pay a { bonus. " + RealAd);

        Assert.Contains("reviews every change together", normalized);
    }

    [Fact]
    public void ProseLength_CountsSentencesNotWords()
    {
        // Both are ~40 words; only one of them is a sentence anyone wrote.
        const string css = "background: #505050 border-radius: 4px padding: 25px 20px margin-bottom: 20px "
            + "font-size: 1.1rem text-transform: none display: inline-block background-color: #009edc";

        Assert.True(JobAdTextNormalizer.ProseLength(RealAd) > JobAdTextNormalizer.MinProseChars);
        Assert.True(JobAdTextNormalizer.ProseLength(css) < JobAdTextNormalizer.MinProseChars);
    }

    [Fact]
    public void StripBracedSpans_RemovesNestedBlocksWhole()
    {
        var stripped = JobAdTextNormalizer.StripBracedSpans("a {x {y} z} b");

        Assert.Equal("a b", string.Join(' ', stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }
}
