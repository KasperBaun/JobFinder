using System.Net;
using System.Text;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

public sealed class HtmlAdapterTests
{
    private const string ListPage = """
        <html><body>
          <ul>
            <li class="job-card">
              <a class="job-link" href="/job/123">Senior Software Engineer</a>
              <span class="loc">Copenhagen</span>
            </li>
            <li class="job-card">
              <a class="job-link" href="https://other.example.com/job/456">Platform Engineer</a>
              <span class="loc">Billund</span>
            </li>
          </ul>
        </body></html>
        """;

    private const string DetailPage = """
        <html><body><article>We build with C# and .NET on Azure.</article></body></html>
        """;

    private static PortalConfig Config(bool enrichBody = false) => new(
        Name: "html-test",
        Type: PortalType.Html,
        Enabled: true,
        Endpoint: new Uri("https://careers.example.com/jobs"),
        Html: new HtmlSelectors(
            ListSelector: "li.job-card",
            TitleSelector: "a.job-link",
            LinkSelector: "a.job-link",
            LocationSelector: ".loc"),
        StaticFields: new Dictionary<string, string> { ["company"] = "Example Corp" },
        RateLimitRps: 0,
        EnrichBody: enrichBody);

    [Fact]
    public async Task FetchAsync_Parses_Cards_And_Resolves_Relative_Urls()
    {
        using var http = new HttpClient(new RoutedHandler());
        var adapter = new HtmlAdapter(Config(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Senior Software Engineer", results[0].Title);
        Assert.Equal("https://careers.example.com/job/123", results[0].Url.ToString());
        Assert.Equal("Copenhagen", results[0].Location);
        Assert.All(results, r => Assert.Equal("Example Corp", r.Company));
    }

    [Fact]
    public async Task FetchAsync_EnrichBody_Merges_Detail_Page_Text_Into_Description()
    {
        using var http = new HttpClient(new RoutedHandler());
        var adapter = new HtmlAdapter(Config(enrichBody: true), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains("C# and .NET on Azure", results[0].Description);
    }

    [Fact]
    public async Task FetchAsync_ScopeLinkSelector_UsesCardElementHref()
    {
        // "List item is the anchor" pattern: the card itself carries the href, so linkSelector
        // is ":scope". element.QuerySelector(":scope") returns null per DOM semantics, so the
        // adapter must resolve it to the card element.
        const string page = """
            <html><body>
              <a class="job" href="/jobs/123"><span class="t">Senior .NET Developer</span></a>
              <a class="job" href="/jobs/456"><span class="t">Cloud Engineer</span></a>
            </body></html>
            """;
        var cfg = new PortalConfig(
            Name: "html-scope",
            Type: PortalType.Html,
            Enabled: true,
            Endpoint: new Uri("https://careers.example.com/jobs"),
            Html: new HtmlSelectors(
                ListSelector: "a.job",
                TitleSelector: ".t",
                LinkSelector: ":scope",
                UrlAttribute: "href"),
            RateLimitRps: 0);
        using var http = new HttpClient(new StaticHandler(page));
        var adapter = new HtmlAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Senior .NET Developer", results[0].Title);
        Assert.Equal("https://careers.example.com/jobs/123", results[0].Url.ToString());
    }

    private sealed class StaticHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/html"),
            });
    }

    [Fact]
    public async Task FetchAsync_Without_EnrichBody_Leaves_Description_Empty()
    {
        using var http = new HttpClient(new RoutedHandler());
        var adapter = new HtmlAdapter(Config(enrichBody: false), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(string.Empty, results[0].Description);
    }

    // Serves the list page for the catalog endpoint and the detail page for any /job/ URL.
    private sealed class RoutedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath.Contains("/job/", StringComparison.Ordinal)
                ? DetailPage
                : ListPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/html"),
            });
        }
    }
}
