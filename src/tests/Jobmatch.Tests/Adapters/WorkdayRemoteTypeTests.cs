using System.Net;
using System.Text;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

// Workday states the arrangement as jobPostingInfo.remoteType, which only the CXS *detail* JSON
// carries — the list response has no such field. Enrichment already fetches that detail for the
// description and locations, so the arrangement rides along on the same request. Values below are
// the vocabulary observed on a live tenant 2026-08-06 ("Site Based", "Hybrid"); tenants that never
// configure the field omit it entirely, leaving inference in charge.
public sealed class WorkdayRemoteTypeTests
{
    [Fact]
    public void ParseWorkdayCxs_ReadsRemoteType()
    {
        const string json = """
            {
              "jobPostingInfo": {
                "jobDescription": "<p>Own the corridor.</p>",
                "location": "Poland, Warsaw, 00-839",
                "remoteType": "Site Based"
              }
            }
            """;

        var posting = BaseAdapter.ParseWorkdayCxs(json);

        Assert.NotNull(posting);
        Assert.Equal("Site Based", posting.RemoteType);
    }

    [Fact]
    public void ParseWorkdayCxs_TenantWithoutRemoteType_YieldsNull()
    {
        const string json = """
            { "jobPostingInfo": { "jobDescription": "<p>Build bricks.</p>", "location": "Billund" } }
            """;

        Assert.Null(BaseAdapter.ParseWorkdayCxs(json)!.RemoteType);
    }

    [Fact]
    public async Task EnrichBodies_Workday_StatedHybrid_ReplacesInferredUnknown()
    {
        var handler = new ScriptedHandler(ListPage, Detail("Hybrid"));
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(MaerskLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Hybrid, Assert.Single(results).RemoteMode);
    }

    // "Site Based" is the employer's own answer and outranks a remote cue in the list title, which
    // is all InferRemoteMode had to go on. A wrong Remote would exempt the listing from the radius
    // filter entirely, so the stated value is the safer as well as the truer one.
    [Fact]
    public async Task EnrichBodies_Workday_StatedSiteBased_OutranksARemoteCueInTheTitle()
    {
        var handler = new ScriptedHandler(ListPage, Detail("Site Based"));
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(MaerskLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Onsite, Assert.Single(results).RemoteMode);
    }

    [Fact]
    public async Task EnrichBodies_Workday_NoRemoteType_KeepsTheInferredMode()
    {
        const string detail = """
            { "jobPostingInfo": { "jobDescription": "<p>Coordinate the corridor.</p>", "location": "Warsaw" } }
            """;
        var handler = new ScriptedHandler(ListPage, detail);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(MaerskLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Remote, Assert.Single(results).RemoteMode);
    }

    private const string ListPage = """
        {
          "total": 1,
          "jobPostings": [
            {
              "title": "Customer Success Partner - Remote",
              "externalPath": "/job/Poland-Warsaw/Customer-Success-Partner_R184425-1",
              "locationsText": "Poland, Warsaw, 00-839",
              "postedOn": "Posted Today"
            }
          ]
        }
        """;

    private static string Detail(string remoteType) => $$"""
        {
          "jobPostingInfo": {
            "jobDescription": "<p>Coordinate the corridor.</p>",
            "location": "Poland, Warsaw, 00-839",
            "remoteType": "{{remoteType}}"
          }
        }
        """;

    private static PortalConfig MaerskLike() => new(
        Name: "workday-maersk",
        Type: PortalType.Api,
        Enabled: true,
        Method: "post",
        Endpoint: new Uri("https://maersk.wd3.myworkdayjobs.com/wday/cxs/maersk/Maersk_Careers/jobs"),
        BodyTemplate: new Dictionary<string, object?> { ["searchText"] = "", ["limit"] = 20, ["offset"] = 0 },
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "jobPostings",
            ["id"] = "externalPath",
            ["title"] = "title",
            ["location"] = "locationsText",
            ["url_template"] = "https://maersk.wd3.myworkdayjobs.com/en-US/Maersk_Careers{externalPath}",
            ["posted_at"] = "postedOn",
        },
        StaticFields: new Dictionary<string, string> { ["company"] = "Maersk" },
        RateLimitRps: 0,
        EnrichBody: true);

    private sealed class ScriptedHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_responses.TryDequeue(out var body))
            {
                throw new InvalidOperationException("ScriptedHandler ran out of canned responses");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
