using System.Net;
using Jobmatch.Features.Providers;

namespace Jobmatch.Tests.Features.Providers;

public sealed class SourceDiscoveryServiceTests
{
    // The shape that broke the add-a-source flow: a marketing careers page whose "See all openings"
    // button is the only thing pointing at the actual board.
    private const string CareersPage = """
        <html><head><title>See yourself in Danske Bank</title></head>
        <body>
          <a href="/careers/joining-us">Joining us</a>
          <a href="https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/requisitions">See all openings</a>
          <a href="https://www.linkedin.com/company/danske-bank">LinkedIn</a>
        </body></html>
        """;

    private static SourceDiscoveryService NewService(string body, string contentType = "text/html") =>
        new(new SourceDetectionService(), new StubHandler(body, contentType));

    [Fact]
    public async Task CareersPage_LinkingToAnAts_YieldsThatBoard()
    {
        using var svc = NewService(CareersPage);

        var found = await svc.DiscoverAsync(new Uri("https://danskebank.com/careers"), CancellationToken.None);

        var c = Assert.Single(found);
        Assert.Equal("oracle", c.Kind);
        Assert.Equal(
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions",
            c.Draft.Endpoint!.ToString());
    }

    [Fact]
    public async Task DiscoveredBoard_IsNamedAfterThePageItCameFrom()
    {
        using var svc = NewService(CareersPage);

        var c = (await svc.DiscoverAsync(new Uri("https://careers.danskebank.com/"), CancellationToken.None))[0];

        // The Oracle URL carries only the opaque tenant id "ejqi" — the crawled host is the better name.
        Assert.Equal("Danskebank", c.DisplayName);
        Assert.Equal("oracle-danskebank", c.Draft.Name);
    }

    [Fact]
    public async Task EscapedLinksInsideScriptPayloads_AreStillFound()
    {
        using var svc = NewService(
            """<script>window.__DATA__ = {"url":"https:\/\/jobs.lever.co\/acmewidgets"};</script>""");

        var c = Assert.Single(await svc.DiscoverAsync(new Uri("https://example.com/jobs"), CancellationToken.None));

        Assert.Equal("lever", c.Kind);
    }

    [Fact]
    public async Task AtsBoardOutranksAFeedOnTheSamePage()
    {
        using var svc = NewService(
            """
            <a href="https://example.com/jobs/feed">Feed</a>
            <a href="https://boards.greenhouse.io/monzo">Openings</a>
            """);

        var found = await svc.DiscoverAsync(new Uri("https://example.com/careers"), CancellationToken.None);

        Assert.Equal(2, found.Count);
        Assert.Equal("greenhouse", found[0].Kind);
        Assert.Equal("rss", found[1].Kind);
    }

    [Fact]
    public async Task PageWithNoRecognisableLinks_YieldsNothing()
    {
        using var svc = NewService("<html><body><a href=\"/about\">About us</a></body></html>");

        Assert.Empty(await svc.DiscoverAsync(new Uri("https://example.com/careers"), CancellationToken.None));
    }

    [Fact]
    public async Task NonHtmlResponse_IsNotScanned()
    {
        using var svc = NewService("%PDF-1.4 https://boards.greenhouse.io/monzo", "application/pdf");

        Assert.Empty(await svc.DiscoverAsync(new Uri("https://example.com/brochure.pdf"), CancellationToken.None));
    }

    [Fact]
    public async Task UnreachablePage_YieldsNothingRatherThanThrowing()
    {
        using var svc = new SourceDiscoveryService(new SourceDetectionService(), new ThrowingHandler());

        Assert.Empty(await svc.DiscoverAsync(new Uri("https://example.com/careers"), CancellationToken.None));
    }

    private sealed class StubHandler(string body, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("no route to host");
    }
}
