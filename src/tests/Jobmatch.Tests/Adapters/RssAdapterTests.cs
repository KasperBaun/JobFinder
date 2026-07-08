using System.Net;
using System.Text;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

// FeedReader's static ReadAsync does its own HTTP, so we can't stub a feed end-to-end
// without standing up a local listener. These tests exercise the body-enrichment path
// directly via internal helpers; the feed-parse path is covered indirectly by live use.
public sealed class RssAdapterTests
{
    private static Listing MakeListing(string url, string description) => new(
        Id: "id",
        Portal: "p",
        Title: "Software Engineer",
        Company: null,
        Location: null,
        RemoteMode: RemoteMode.Unknown,
        Description: description,
        Url: new Uri(url),
        PostedAt: null,
        FetchedAt: DateTimeOffset.UtcNow,
        Raw: JsonDocument.Parse("{}").RootElement.Clone());

    private static PortalConfig RssConfig(bool enrich) => new(
        Name: "it-jobbank-rss",
        Type: PortalType.Rss,
        Enabled: true,
        Endpoint: new Uri("https://example.com/feed.rss"),
        EnrichBody: enrich);

    [Fact]
    public void MergeBodyHtml_NullBody_ReturnsOriginal()
    {
        var original = MakeListing("https://example.com/1", "short rss desc");
        var result = RssAdapter.MergeBodyHtml(original, null);
        Assert.Same(original, result);
    }

    [Fact]
    public void MergeBodyHtml_EmptyBody_ReturnsOriginal()
    {
        var original = MakeListing("https://example.com/1", "short rss desc");
        var result = RssAdapter.MergeBodyHtml(original, "   \n\t  ");
        Assert.Same(original, result);
    }

    [Fact]
    public void MergeBodyHtml_AppendsStrippedBody_ToExistingDescription()
    {
        var original = MakeListing("https://example.com/1", "RSS summary.");
        var bodyHtml = "<html><body><h1>Job</h1><p>We use <b>C#</b> and <em>TypeScript</em>.</p></body></html>";
        var result = RssAdapter.MergeBodyHtml(original, bodyHtml);

        Assert.NotSame(original, result);
        Assert.StartsWith("RSS summary.", result.Description);
        Assert.Contains("C#", result.Description);
        Assert.Contains("TypeScript", result.Description);
    }

    [Fact]
    public void MergeBodyHtml_EmptyOriginalDescription_TakesBodyAsIs()
    {
        var original = MakeListing("https://example.com/1", string.Empty);
        var bodyHtml = "<p>We use Azure.</p>";
        var result = RssAdapter.MergeBodyHtml(original, bodyHtml);

        Assert.Equal("We use Azure.", result.Description);
    }

    [Fact]
    public void MergeBodyHtml_BodyOfOnlyMarkup_LeavesOriginalAlone()
    {
        var original = MakeListing("https://example.com/1", "RSS summary.");
        var bodyHtml = "<html><body></body></html>";
        var result = RssAdapter.MergeBodyHtml(original, bodyHtml);

        Assert.Same(original, result);
    }

    [Fact]
    public async Task EnrichBodiesAsync_FetchesEachListingsBody_AndMergesIntoDescription()
    {
        const string body = "<p>Job uses <strong>Azure</strong> and Kubernetes.</p>";
        using var http = new HttpClient(new HtmlStub(HttpStatusCode.OK, body));
        var adapter = new RssAdapter(RssConfig(enrich: true), http, NullLogger.Instance);

        var input = new[]
        {
            MakeListing("https://example.com/job1", "rss summary 1"),
            MakeListing("https://example.com/job2", "rss summary 2"),
        };

        var enriched = await adapter.EnrichBodiesAsync(input, CancellationToken.None);

        Assert.Equal(2, enriched.Count);
        Assert.All(enriched, l => Assert.Contains("Azure", l.Description));
        Assert.All(enriched, l => Assert.Contains("Kubernetes", l.Description));
    }

    [Fact]
    public async Task EnrichBodiesAsync_BadResponse_KeepsOriginalListing()
    {
        // 500 from the listing host shouldn't drop the listing — we keep the RSS-only
        // version and let the next signal (title / freshness / location) carry it.
        using var http = new HttpClient(new HtmlStub(HttpStatusCode.InternalServerError, "boom"));
        var adapter = new RssAdapter(RssConfig(enrich: true), http, NullLogger.Instance);

        var input = new[] { MakeListing("https://example.com/job1", "rss summary") };

        var enriched = await adapter.EnrichBodiesAsync(input, CancellationToken.None);

        Assert.Single(enriched);
        Assert.Equal("rss summary", enriched[0].Description);
    }

    [Fact]
    public async Task EnrichBodiesAsync_NonHtmlContent_LeavesOriginalAlone()
    {
        using var http = new HttpClient(new HtmlStub(HttpStatusCode.OK, "{\"x\":1}", "application/json"));
        var adapter = new RssAdapter(RssConfig(enrich: true), http, NullLogger.Instance);

        var input = new[] { MakeListing("https://example.com/job1", "rss summary") };

        var enriched = await adapter.EnrichBodiesAsync(input, CancellationToken.None);

        Assert.Single(enriched);
        Assert.Equal("rss summary", enriched[0].Description);
    }

    [Fact]
    public async Task EnrichBodiesAsync_RunsConcurrently_ButPreservesInputOrder()
    {
        // Enrichment fetches concurrently; the stub echoes each request's path and delays the
        // earlier listings longer, so a naive completion-order collector would reorder them. The
        // contract is that output[i] still corresponds to input[i].
        using var http = new HttpClient(new PathEchoStub());
        var adapter = new RssAdapter(RssConfig(enrich: true), http, NullLogger.Instance);

        var input = Enumerable.Range(0, 12)
            .Select(i => MakeListing($"https://example.com/job{i}", $"summary{i}"))
            .ToArray();

        var enriched = await adapter.EnrichBodiesAsync(input, CancellationToken.None);

        Assert.Equal(input.Length, enriched.Count);
        for (var i = 0; i < input.Length; i++)
        {
            Assert.StartsWith($"summary{i}", enriched[i].Description);
            Assert.Contains($"marker-job{i}", enriched[i].Description);
        }
    }

    [Fact]
    public async Task EnrichBodiesAsync_CancellationRequested_Propagates()
    {
        // A per-source budget cancels via the token; enrichment must surface that as an
        // OperationCanceledException (so the caller can mark the source timed out), not swallow
        // it as a per-item "keep the listing" failure.
        using var http = new HttpClient(new HtmlStub(HttpStatusCode.OK, "<p>body</p>"));
        var adapter = new RssAdapter(RssConfig(enrich: true), http, NullLogger.Instance);

        var input = new[] { MakeListing("https://example.com/job1", "rss summary") };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.EnrichBodiesAsync(input, cts.Token));
    }

    [Theory]
    [InlineData("https://www.jobindex.dk/vis-job/h1646496",
                "Senior .Net udvikler til afdeling i vækst, Sopra Steria A/S",
                "Senior .Net udvikler til afdeling i vækst", "Sopra Steria A/S")]
    [InlineData("https://www.jobindex.dk/vis-job/h1662925",
                "Softwareudvikler – byg fundamentet for Danske Spils digitale platform med AI-first udvikling, Danske Spil A/S",
                "Softwareudvikler – byg fundamentet for Danske Spils digitale platform med AI-first udvikling", "Danske Spil A/S")]
    [InlineData("https://www.it-jobbank.dk/jobannonce/123",
                "Senior Dev, Acme ApS",
                "Senior Dev", "Acme ApS")]
    [InlineData("https://www.jobindex.dk/vis-job/h999",
                "Erfaren softwareudvikler til DR Teknologi, DR",
                "Erfaren softwareudvikler til DR Teknologi", "DR")]
    public void ExtractJobindexTrailingCompany_OnJobindexHost_SplitsTrailingCompany(
        string url, string title, string expectedTitle, string expectedCompany)
    {
        var (cleanTitle, company) = RssAdapter.ExtractJobindexTrailingCompany(title, new Uri(url));
        Assert.Equal(expectedTitle, cleanTitle);
        Assert.Equal(expectedCompany, company);
    }

    [Theory]
    [InlineData("https://example.com/jobs/1", "Senior Engineer, Cloud Platform")]
    [InlineData("https://other-rss.example/feed/item", "Lead Developer, Data & AI")]
    public void ExtractJobindexTrailingCompany_OffJobindex_IsNoOp(string url, string title)
    {
        var (cleanTitle, company) = RssAdapter.ExtractJobindexTrailingCompany(title, new Uri(url));
        Assert.Equal(title, cleanTitle);
        Assert.Null(company);
    }

    [Fact]
    public void ExtractJobindexTrailingCompany_NoComma_ReturnsOriginal()
    {
        var (cleanTitle, company) = RssAdapter.ExtractJobindexTrailingCompany(
            "Backend Developer", new Uri("https://www.jobindex.dk/vis-job/h1"));
        Assert.Equal("Backend Developer", cleanTitle);
        Assert.Null(company);
    }

    private sealed class HtmlStub(HttpStatusCode status, string body, string contentType = "text/html") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
    }

    // Echoes the request path (e.g. /job7 -> "marker-job7") and delays earlier listings longer,
    // so completion order is the reverse of input order. Lets the order test prove that concurrent
    // enrichment still reassembles results positionally.
    private sealed class PathEchoStub : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var segment = request.RequestUri!.AbsolutePath.TrimStart('/');
            var n = int.Parse(new string(segment.Where(char.IsDigit).ToArray()));
            await Task.Delay((20 - n) * 5, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"<p>marker-{segment}</p>", Encoding.UTF8, "text/html"),
            };
        }
    }
}
