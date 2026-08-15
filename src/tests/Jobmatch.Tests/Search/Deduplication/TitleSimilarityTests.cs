using Jobmatch.Search.Deduplication;

namespace Jobmatch.Tests.Search.Deduplication;

public sealed class TitleSimilarityTests
{
    private static HashSet<string> Tokens(string title) => TitleSimilarity.Tokenise(Deduper.Normalise(title));

    [Fact]
    public void Tokenise_Keeps_Language_Tokens_Intact()
    {
        var tokens = Tokens("Senior Software Engineer- (C#, APL) Valuation Product Area");
        Assert.Contains("c#", tokens);
        Assert.Contains("apl", tokens);
        Assert.Contains("engineer", tokens);
        Assert.DoesNotContain("engineer-", tokens);
    }

    [Fact]
    public void Tokenise_Splits_Slash_Compounds()
    {
        var tokens = Tokens("Senior/Lead Full-Stack Developer (.Net/Angular)");
        Assert.Contains("senior", tokens);
        Assert.Contains("lead", tokens);
        Assert.Contains(".net", tokens);
        Assert.Contains("angular", tokens);
        Assert.Contains("full", tokens);
        Assert.Contains("stack", tokens);
    }

    [Theory]
    [InlineData("C# Developer", "C# Developer", 1.0)]
    [InlineData("C# Developer", "Java Developer", 1.0 / 3)]
    [InlineData("C# Developer", "Gardener", 0.0)]
    public void Jaccard_Measures_Token_Overlap(string a, string b, double expected)
    {
        Assert.Equal(expected, TitleSimilarity.Jaccard(Tokens(a), Tokens(b)), precision: 5);
    }

    [Theory]
    [InlineData("Senior .Net udvikler til afdeling i vækst", "Senior Fullstack Java udvikler til afdeling i vækst", true)]
    [InlineData("React Developer", "Angular Developer", true)]
    [InlineData("Cloud Engineer (Azure)", "Cloud Engineer (AWS)", true)]
    [InlineData("Senior C# Developer", "Senior .NET Developer", false)]
    [InlineData("Senior Developer", "Senior .NET Developer", false)]
    [InlineData("Full-Stack Engineer (.Net/Angular)", "Full-Stack Engineer (.Net)", false)]
    [InlineData("Softwareudvikler", "Softwareudvikler", false)]
    public void StackConflicts_Only_Across_Diverging_Families(string a, string b, bool conflict)
    {
        Assert.Equal(conflict, TitleSimilarity.StackConflicts(Tokens(a), Tokens(b)));
    }

    [Theory]
    [InlineData("Senior Engineer", "Lead Engineer", true)]
    [InlineData("Junior Developer", "Senior Developer", true)]
    [InlineData("Senior Engineer", "Senior/Lead Engineer", false)]
    [InlineData("Senior Engineer", "Engineer", false)]
    [InlineData("Engineer", "Engineer", false)]
    [InlineData("Student Assistant", "Senior Assistant", true)]
    public void SeniorityConflicts_Only_When_Signatures_Diverge(string a, string b, bool conflict)
    {
        Assert.Equal(conflict, TitleSimilarity.SeniorityConflicts(Tokens(a), Tokens(b)));
    }
}
