using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

// HtmlAdapter fetches each list page through the injected HttpClient, so the page loop
// (BaseAdapter.FetchPagesAsync, driven by Config.Pagination) is fully stubbable. Nordea
// pages via ?startrow=N in steps of 100.
public sealed class HtmlAdapterPaginationTests
{
    private static PortalConfig Config(PaginationConfig? pagination) => new(
        Name: "html-nordea",
        Type: PortalType.Html,
        Enabled: true,
        Endpoint: new Uri("https://careers.example.com/search-jobs"),
        Html: new HtmlSelectors(
            ListSelector: "tr.data-row",
            TitleSelector: "a.jobTitle-link",
            LinkSelector: "a.jobTitle-link",
            UrlAttribute: "href"),
        Pagination: pagination,
        RateLimitRps: 0,
        EnrichBody: false);

    private static string ListPage(params (string title, string id)[] rows)
    {
        var sb = new StringBuilder("<html><body><table>");
        foreach (var (title, id) in rows)
        {
            sb.Append($"<tr class=\"data-row\"><td><a class=\"jobTitle-link\" href=\"/job/{id}\">{title}</a></td></tr>");
        }
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    [Fact]
    public async Task FetchAsync_WalksPages_UntilEmpty_AndSendsStartrowCursor()
    {
        var handler = new PagedListHandler(startrow => startrow switch
        {
            0 => ListPage(("Engineer A", "a"), ("Engineer B", "b")),
            2 => ListPage(("Engineer C", "c"), ("Engineer D", "d")),
            _ => ListPage(), // beyond => empty => stop
        });
        using var http = new HttpClient(handler);
        var adapter = new HtmlAdapter(
            Config(new PaginationConfig(Param: "startrow", Start: 0, Step: 2, Size: 2, MaxPages: 5)),
            http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(4, results.Count);
        Assert.Equal(new[] { "Engineer A", "Engineer B", "Engineer C", "Engineer D" }, results.Select(r => r.Title));
        Assert.Equal(new[] { 0, 2, 4 }, handler.RequestedStartrows);
    }

    [Fact]
    public async Task FetchAsync_StopsWhenPageAddsNothingNew_ServerIgnoresCursor()
    {
        // Pandora-style: ?page/?startrow ignored, same rows re-served every request.
        var handler = new PagedListHandler(_ => ListPage(("Engineer A", "a"), ("Engineer B", "b")));
        using var http = new HttpClient(handler);
        var adapter = new HtmlAdapter(
            Config(new PaginationConfig(Param: "startrow", Start: 0, Step: 2, Size: 2, MaxPages: 5)),
            http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, handler.RequestedStartrows.Count); // first new, second all-dupes => stop
    }

    [Fact]
    public async Task FetchAsync_NoPaginationConfig_DoesSingleFetch()
    {
        var handler = new PagedListHandler(_ => ListPage(("Engineer A", "a"), ("Engineer B", "b")));
        using var http = new HttpClient(handler);
        var adapter = new HtmlAdapter(Config(pagination: null), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Single(handler.RequestedStartrows);
    }

    private sealed class PagedListHandler(Func<int, string> pageForStartrow) : HttpMessageHandler
    {
        public List<int> RequestedStartrows { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var m = Regex.Match(request.RequestUri!.Query, @"[?&]startrow=(\d+)");
            var startrow = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            RequestedStartrows.Add(startrow);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pageForStartrow(startrow), Encoding.UTF8, "text/html"),
            });
        }
    }
}
