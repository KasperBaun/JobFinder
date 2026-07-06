using System.Net;
using System.Text;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

// The Workday CXS shape used by the workday-* catalog entries: POST with a flat
// body, offset/limit pagination injected into the body, items under jobPostings,
// url built from {externalPath}, relative postedOn text, locationsText sometimes absent.
public sealed class ApiAdapterWorkdayTests
{
    private static PortalConfig WorkdayLike() => new(
        Name: "workday-lego",
        Type: PortalType.Api,
        Enabled: true,
        Method: "post",
        Endpoint: new Uri("https://lego.wd103.myworkdayjobs.com/wday/cxs/lego/LEGO_External/jobs"),
        BodyTemplate: new Dictionary<string, object?>
        {
            ["searchText"] = "software",
            ["limit"] = 20,
            ["offset"] = 0,
        },
        Pagination: new PaginationConfig(Param: "offset", Start: 0, Step: 20, SizeParam: "limit", Size: 20, MaxPages: 3),
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "jobPostings",
            ["id"] = "externalPath",
            ["title"] = "title",
            ["location"] = "locationsText",
            ["url_template"] = "https://lego.wd103.myworkdayjobs.com/en-US/LEGO_External{externalPath}",
            ["posted_at"] = "postedOn",
        },
        StaticFields: new Dictionary<string, string> { ["company"] = "LEGO" },
        RateLimitRps: 0);

    private const string PageOne = """
        {
          "total": 2,
          "jobPostings": [
            {
              "title": "Senior Software Engineer",
              "externalPath": "/job/Billund/Senior-Software-Engineer_R123",
              "locationsText": "Billund, Denmark",
              "postedOn": "Posted Today"
            },
            {
              "title": "Platform Engineer",
              "externalPath": "/job/Copenhagen/Platform-Engineer_R456",
              "postedOn": "Posted 17 Days Ago"
            }
          ]
        }
        """;

    private const string EmptyPage = """{ "total": 2, "jobPostings": [] }""";

    [Fact]
    public async Task FetchAsync_Workday_Shape_Maps_Items_And_Injects_Offset_Into_Body()
    {
        var handler = new ScriptedHandler(PageOne, EmptyPage);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(WorkdayLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Senior Software Engineer", results[0].Title);
        Assert.Equal(
            "https://lego.wd103.myworkdayjobs.com/en-US/LEGO_External/job/Billund/Senior-Software-Engineer_R123",
            results[0].Url.ToString());
        Assert.All(results, r => Assert.Equal("LEGO", r.Company));

        // 2 items against a page size of 20 = partial page → pagination stops after
        // one request, with offset/limit injected into the POST body.
        Assert.Single(handler.RequestBodies);
        var body0 = JsonDocument.Parse(handler.RequestBodies[0]!);
        Assert.Equal(0, body0.RootElement.GetProperty("offset").GetInt32());
        Assert.Equal(20, body0.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task FetchAsync_Workday_Relative_PostedOn_Yields_Null_PostedAt()
    {
        var handler = new ScriptedHandler(PageOne, EmptyPage);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(WorkdayLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.All(results, r => Assert.Null(r.PostedAt));
    }

    [Fact]
    public async Task FetchAsync_Workday_ClampedOffset_StopsOnDuplicatePage_NoOverFetch()
    {
        // Workday clamps an out-of-range offset and re-serves an earlier FULL page instead of an
        // empty/short one, so the short-page break never fires. With a page size of 2 the two-job
        // page is "full"; the loop must stop once a page adds nothing new — returning the jobs once,
        // not MaxPages x the same page (and, with enrichBody, not enriching duplicates).
        var config = WorkdayLike() with
        {
            Pagination = new PaginationConfig(Param: "offset", Start: 0, Step: 2, SizeParam: "limit", Size: 2, MaxPages: 5),
        };
        var handler = new ScriptedHandler(PageOne, PageOne, PageOne, PageOne, PageOne);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(config, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count); // deduped, not 5 pages x 2 jobs
        Assert.Equal(2, handler.RequestBodies.Count); // page 1 (new), page 2 (all dupes => stop)
    }

    [Fact]
    public async Task FetchAsync_Workday_Missing_LocationsText_Keeps_Item_With_Null_Location()
    {
        var handler = new ScriptedHandler(PageOne, EmptyPage);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(WorkdayLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal("Billund, Denmark", results[0].Location);
        Assert.Null(results[1].Location);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        public List<string?> RequestBodies { get; } = [];

        public ScriptedHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (!_responses.TryDequeue(out var body))
            {
                throw new InvalidOperationException(
                    $"ScriptedHandler ran out of canned responses after {RequestBodies.Count} call(s)");
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
