using System.Net;
using System.Text;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

// RssAdapter fetches feed bytes through the injected HttpClient (GetStringAsync +
// FeedReader.ReadFromString), so the page loop is fully stubbable. These cover the
// Config.Pagination path shared via BaseAdapter.FetchPagesAsync.
public sealed class RssAdapterPaginationTests
{
    private static PortalConfig Config(PaginationConfig? pagination) => new(
        Name: "jobindex-rss",
        Type: PortalType.Rss,
        Enabled: true,
        Endpoint: new Uri("https://www.jobindex.dk/jobsoegning.rss"),
        QueryParams: new Dictionary<string, object?> { ["q"] = "+c#" },
        Pagination: pagination,
        RateLimitRps: 0,
        EnrichBody: false);

    private static string Feed(params (string title, string id)[] items)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><rss version=\"2.0\"><channel>");
        sb.Append("<title>Jobindex</title><link>https://www.jobindex.dk</link><description>d</description>");
        foreach (var (title, id) in items)
        {
            sb.Append($"<item><title>{title}</title><link>https://www.jobindex.dk/vis-job/{id}</link><guid>{id}</guid></item>");
        }
        sb.Append("</channel></rss>");
        return sb.ToString();
    }

    [Fact]
    public async Task FetchAsync_WalksPages_UntilEmptyPage_AndSendsPageCursor()
    {
        var handler = new PagedFeedHandler(page => page switch
        {
            1 => Feed(("Job A", "a"), ("Job B", "b")),
            2 => Feed(("Job C", "c"), ("Job D", "d")),
            _ => Feed(), // page 3 empty => stop
        });
        using var http = new HttpClient(handler);
        var adapter = new RssAdapter(
            Config(new PaginationConfig(Param: "page", Start: 1, Step: 1, Size: 2, MaxPages: 8)),
            http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(4, results.Count);
        Assert.Equal(new[] { "Job A", "Job B", "Job C", "Job D" }, results.Select(r => r.Title));
        // 3 requests: page 1, 2 (full), 3 (empty, stops). Page cursor and base query both present.
        Assert.Equal(3, handler.RequestedPages.Count);
        Assert.Equal(new[] { 1, 2, 3 }, handler.RequestedPages);
        Assert.All(handler.RequestedUris, u => Assert.Contains("q=%2Bc%23", u));
    }

    [Fact]
    public async Task FetchAsync_StopsWhenPageAddsNothingNew_ServerIgnoresCursor()
    {
        // Server ignores ?page= and re-serves page 1 forever. The no-new-items guard
        // must stop after the second request instead of looping to MaxPages.
        var handler = new PagedFeedHandler(_ => Feed(("Job A", "a"), ("Job B", "b")));
        using var http = new HttpClient(handler);
        var adapter = new RssAdapter(
            Config(new PaginationConfig(Param: "page", Start: 1, Step: 1, Size: 2, MaxPages: 8)),
            http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, handler.RequestedPages.Count); // page 1 (new), page 2 (all dupes => stop)
    }

    [Fact]
    public async Task FetchAsync_RespectsMaxPages()
    {
        var handler = new PagedFeedHandler(page => Feed(($"Job {page}a", $"{page}a"), ($"Job {page}b", $"{page}b")));
        using var http = new HttpClient(handler);
        var adapter = new RssAdapter(
            Config(new PaginationConfig(Param: "page", Start: 1, Step: 1, Size: 2, MaxPages: 3)),
            http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(6, results.Count);            // 3 pages x 2 distinct items
        Assert.Equal(3, handler.RequestedPages.Count);
    }

    [Fact]
    public async Task FetchAsync_NoPaginationConfig_DoesSingleFetch()
    {
        var handler = new PagedFeedHandler(_ => Feed(("Job A", "a"), ("Job B", "b")));
        using var http = new HttpClient(handler);
        var adapter = new RssAdapter(Config(pagination: null), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Single(handler.RequestedPages);
    }

    private sealed class PagedFeedHandler(Func<int, string> feedForPage) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];
        public List<string> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            RequestedUris.Add(uri.Query);
            var m = System.Text.RegularExpressions.Regex.Match(uri.Query, @"[?&]page=(\d+)");
            var page = m.Success ? int.Parse(m.Groups[1].Value) : 1;
            RequestedPages.Add(page);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feedForPage(page), Encoding.UTF8, "application/rss+xml"),
            });
        }
    }
}
