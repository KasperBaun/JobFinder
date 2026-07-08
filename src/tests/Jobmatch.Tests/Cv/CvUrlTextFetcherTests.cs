using Jobmatch.Cv;

namespace Jobmatch.Tests.Cv;

public sealed class CvUrlTextFetcherTests
{
    [Fact]
    public void HtmlToText_StripsScriptStyleAndChrome()
    {
        const string html = """
            <html><head><style>.x{color:red}</style></head><body>
            <nav>Menu Home About</nav>
            <script>alert('x')</script>
            <main><h1>Jane Doe</h1><p>Backend Developer — C#, .NET</p></main>
            <footer>© 2026</footer>
            </body></html>
            """;

        var text = CvUrlTextFetcher.HtmlToText(html);

        Assert.Contains("Jane Doe", text);
        Assert.Contains("Backend Developer", text);
        Assert.DoesNotContain("alert", text);
        Assert.DoesNotContain("color:red", text);
        Assert.DoesNotContain("Menu Home", text);
        Assert.DoesNotContain("©", text);
    }

    [Fact]
    public void HtmlToText_NoBody_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CvUrlTextFetcher.HtmlToText(""));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/cv.pdf")]
    [InlineData("file:///etc/passwd")]
    public async Task FetchAsync_NonHttpUrl_ThrowsInvalidRequest(string url)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => CvUrlTextFetcher.FetchAsync(url));
    }
}
