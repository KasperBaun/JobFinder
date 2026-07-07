using Jobmatch.Cv;

namespace Jobmatch.Tests.Cv;

public sealed class CvTextNormalizerTests
{
    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        var result = CvTextNormalizer.Normalize("Jane\t\t Doe\r\n\r\n\r\n\r\nBackend   Developer");

        Assert.Equal("Jane Doe\n\nBackend Developer", result);
    }

    [Fact]
    public void Normalize_LongText_TruncatesHeadFirstWithMarker()
    {
        // 'x', not 'a': under a Danish locale the culture-aware StartsWith treats "aa" as a
        // contraction of "å" and the prefix comparison fails.
        var result = CvTextNormalizer.Normalize(new string('x', CvTextNormalizer.MaxChars + 500));

        Assert.EndsWith("[…truncated]", result, StringComparison.Ordinal);
        Assert.StartsWith("xxx", result, StringComparison.Ordinal);
        Assert.True(result.Length < CvTextNormalizer.MaxChars + 50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Normalize_Blank_ReturnsEmpty(string raw)
    {
        Assert.Equal(string.Empty, CvTextNormalizer.Normalize(raw));
    }
}
