using System.Net;
using System.Text;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Fetching.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Search.Fetching.Adapters;

// Truncated multi-site location cells. Markup captured verbatim from careers.nordea.com
// (SuccessFactors) on 2026-08-05: the list row names the first site plus a "+N more…"
// affordance, and only the job page carries every site — as schema.org microdata, not JSON-LD.
public sealed class HtmlAdapterLocationTests
{
    private const string ListPage = """
        <html><body><table><tbody>
        <tr class="data-row">
            <td class="colTitle" headers="hdrTitle">
                <span class="jobTitle hidden-phone">
                    <a href="/job/Helsinki-Senior-Desk-Quant%2C-Nordea-Markets%2C-Helsinki-or-Copenhagen-00500/1413193633/" class="jobTitle-link">Senior Desk Quant, Nordea Markets, Helsinki or Copenhagen</a>
                </span>
                <div class="jobdetail-phone visible-phone">
                    <span class="jobTitle visible-phone">
                        <a class="jobTitle-link" href="/job/Helsinki-Senior-Desk-Quant%2C-Nordea-Markets%2C-Helsinki-or-Copenhagen-00500/1413193633/">Senior Desk Quant, Nordea Markets, Helsinki or Copenhagen</a>
                    </span>
                    <span class="jobLocation visible-phone">

        <span class="jobLocation">
            Helsinki, FI, 00500

                <small class="nobr">+1 more…</small>
        </span></span>
                </div>
            </td>
            <td class="colLocation hidden-phone" headers="hdrLocation">

        <span class="jobLocation">
            Helsinki, FI, 00500

                <small class="nobr">+1 more…</small>
        </span>
            </td>
            <td class="hidden-phone"></td>
        </tr>
        </tbody></table></body></html>
        """;

    private const string JobPage = """
        <html><body>
        <div class="jobDisplayShell" itemscope="itemscope" itemtype="http://schema.org/JobPosting"><span itemprop="jobLocation" itemscope itemtype="http://schema.org/Place"><span itemprop="address" itemscope itemtype="http://schema.org/PostalAddress"><meta itemprop="streetAddress" content="København S, DK, 2300"></span><span itemprop="address" itemscope itemtype="http://schema.org/PostalAddress"><meta itemprop="streetAddress" content="Helsinki, FI, 00500"></span></span><meta itemprop="datePosted" content="Thu Jul 09 00:00:00 UTC 2026"><meta itemprop="hiringOrganization" content="nordeabank">
            <p id="job-location" class="jobLocation job-location-inline">
                <span class="jobGeoLocation">Helsinki, FI, 00500</span>
                <span class="jobGeoLocation">København S, DK, 2300</span>
            </p>
            <span itemprop="description" class="rtltextaligneligible">Our team of five quants is located on our Copenhagen and Helsinki trading floors.</span>
        </div>
        </body></html>
        """;

    private static PortalConfig NordeaLike(bool enrichBody = true) => new(
        Name: "html-nordea",
        Type: PortalType.Html,
        Enabled: true,
        Endpoint: new Uri("https://careers.nordea.com/search-jobs"),
        Html: new HtmlSelectors(
            ListSelector: "tr.data-row",
            TitleSelector: "a.jobTitle-link",
            LinkSelector: "a.jobTitle-link",
            LocationSelector: ".jobLocation",
            UrlAttribute: "href"),
        StaticFields: new Dictionary<string, string> { ["company"] = "Nordea" },
        RateLimitRps: 0,
        EnrichBody: enrichBody);

    [Fact]
    public async Task FetchAsync_RecoversEverySiteOfATruncatedLocationFromTheJobPage()
    {
        using var http = new HttpClient(new RoutedHandler());
        var adapter = new HtmlAdapter(NordeaLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        var listing = Assert.Single(results);
        Assert.Equal("København S, DK, 2300 / Helsinki, FI, 00500", listing.Location);
    }

    [Fact]
    public async Task FetchAsync_WithoutEnrichment_KeepsTheNamedSiteWithoutTheAffordance()
    {
        using var http = new HttpClient(new RoutedHandler());
        var adapter = new HtmlAdapter(NordeaLike(enrichBody: false), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal("Helsinki, FI, 00500", Assert.Single(results).Location);
    }

    [Fact]
    public async Task FetchAsync_UnreachableJobPage_StillStripsTheAffordance()
    {
        using var http = new HttpClient(new RoutedHandler(jobPageStatus: HttpStatusCode.NotFound));
        var adapter = new HtmlAdapter(NordeaLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal("Helsinki, FI, 00500", Assert.Single(results).Location);
    }

    [Theory]
    [InlineData("Helsinki, FI, 00500\n            \n                +1 more…", true)]
    [InlineData("Taastrup, DK, 2630 +2 more...", true)]
    [InlineData("Oslo, NO, 0368 +12 MORE…", true)]
    [InlineData("Copenhagen", false)]
    [InlineData("Aarhus C, DK, 8000", false)]
    [InlineData("Rambøll +1", false)]
    public void IsMissingOrPlaceholderLocation_TreatsATruncatedCellAsIncomplete(string location, bool expected)
    {
        Assert.Equal(expected, BaseAdapter.IsMissingOrPlaceholderLocation(location));
    }

    [Theory]
    [InlineData("Helsinki, FI, 00500\n            \n                +1 more…", "Helsinki, FI, 00500")]
    [InlineData("Taastrup, DK, 2630 +2 more...", "Taastrup, DK, 2630")]
    [InlineData("Copenhagen", "Copenhagen")]
    [InlineData(null, null)]
    public void StripTruncationAffordance_RemovesOnlyTheAffordance(string? location, string? expected)
    {
        Assert.Equal(expected, BaseAdapter.StripTruncationAffordance(location));
    }

    [Fact]
    public void ExtractMicrodataLocation_ReadsEveryAddressUnderJobLocation()
    {
        Assert.Equal("København S, DK, 2300 / Helsinki, FI, 00500", BaseAdapter.ExtractMicrodataLocation(JobPage));
    }

    [Fact]
    public void ExtractMicrodataLocation_PrefersLocalityAndCountryWhenMarkedUp()
    {
        const string html = """
            <div itemscope itemtype="http://schema.org/JobPosting">
              <span itemprop="jobLocation" itemscope itemtype="http://schema.org/Place">
                <span itemprop="address" itemscope itemtype="http://schema.org/PostalAddress">
                  <meta itemprop="streetAddress" content="Nordea Alle 1">
                  <span itemprop="addressLocality">Taastrup</span>
                  <meta itemprop="addressCountry" content="DK">
                </span>
              </span>
            </div>
            """;

        Assert.Equal("Taastrup, DK", BaseAdapter.ExtractMicrodataLocation(html));
    }

    [Fact]
    public void ExtractMicrodataLocation_NoJobPosting_ReturnsNull()
    {
        Assert.Null(BaseAdapter.ExtractMicrodataLocation("<html><body>plain</body></html>"));
        Assert.Null(BaseAdapter.ExtractMicrodataLocation(null));
    }

    private sealed class RoutedHandler(HttpStatusCode jobPageStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isJobPage = request.RequestUri!.AbsolutePath.StartsWith("/job/", StringComparison.Ordinal);
            if (isJobPage && jobPageStatus != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(jobPageStatus));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(isJobPage ? JobPage : ListPage, Encoding.UTF8, "text/html"),
            });
        }
    }
}
