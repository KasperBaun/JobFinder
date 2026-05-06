using System.Net;
using System.Text;
using System.Text.Json;
using Jobmatch;
using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

public sealed class ApiAdapterTests
{
    private static PortalConfig JobnetLike(string? urlTemplate = null) => new(
        Name: "jobnet",
        Type: PortalType.Api,
        Enabled: true,
        Endpoint: new Uri("https://example.com/search"),
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "JobPositionPostings",
            ["id"] = "Id",
            ["title"] = "Title",
            ["company"] = "HiringOrgName",
            ["location"] = "WorkPlaceCity",
            ["description"] = "Description",
            ["url_template"] = urlTemplate ?? "https://example.com/jobs/{Id}",
            ["posted_at"] = "PostingCreated",
        });

    private const string HappyPathPayload = """
        {
          "JobPositionPostings": [
            {
              "Id": "123",
              "Title": "Senior .NET Engineer",
              "HiringOrgName": "Contoso",
              "WorkPlaceCity": "Copenhagen",
              "Description": "We need <b>C#</b> skills &amp; cloud experience.",
              "PostingCreated": "2026-04-01T12:00:00Z"
            },
            {
              "Id": "456",
              "Title": "Backend Developer",
              "HiringOrgName": "Initech",
              "WorkPlaceCity": "Aarhus",
              "Description": "<p>Kotlin shop.</p>",
              "PostingCreated": "2026-04-05T08:00:00Z"
            }
          ]
        }
        """;

    [Fact]
    public async Task FetchAsync_HappyPath_Maps_All_Items()
    {
        using var http = new HttpClient(new StubHandler(HappyPathPayload));
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal("Senior .NET Engineer", results[0].Title);
        Assert.Equal("Contoso", results[0].Company);
        Assert.Equal("Copenhagen", results[0].Location);
        Assert.Equal("https://example.com/jobs/123", results[0].Url.ToString());
        Assert.Equal("We need C# skills & cloud experience.", results[0].Description);
        Assert.NotNull(results[0].PostedAt);
        Assert.Equal(2026, results[0].PostedAt!.Value.Year);
    }

    [Fact]
    public async Task FetchAsync_Missing_Optional_Fields_Are_Null_Not_Error()
    {
        const string payload = """
            {
              "JobPositionPostings": [
                {
                  "Id": "1",
                  "Title": "Role",
                  "Description": "..."
                }
              ]
            }
            """;
        using var http = new HttpClient(new StubHandler(payload));
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Single(results);
        Assert.Null(results[0].Company);
        Assert.Null(results[0].Location);
        Assert.Null(results[0].PostedAt);
    }

    [Fact]
    public async Task FetchAsync_Missing_Title_Is_Skipped()
    {
        const string payload = """
            {
              "JobPositionPostings": [
                { "Id": "1", "Description": "no title" }
              ]
            }
            """;
        using var http = new HttpClient(new StubHandler(payload));
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task FetchAsync_ItemsPath_Not_Array_Returns_Empty_Not_Throw()
    {
        const string payload = """
            { "JobPositionPostings": { "total": 0 } }
            """;
        using var http = new HttpClient(new StubHandler(payload));
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task FetchAsync_StaticFields_Override_Empty_Mapped_Company()
    {
        const string payload = """
            {
              "JobPositionPostings": [
                { "Id": "1", "Title": "Role", "Description": "..." }
              ]
            }
            """;
        var cfg = JobnetLike() with
        {
            StaticFields = new Dictionary<string, string> { ["company"] = "Pleo" },
        };
        using var http = new HttpClient(new StubHandler(payload));
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Single(results);
        Assert.Equal("Pleo", results[0].Company);
    }

    [Fact]
    public async Task FetchAsync_StaticFields_Override_NonEmpty_Mapped_Company()
    {
        // Static field always wins when non-empty — even over a mapped value.
        // This is the documented precedence: a per-company board may legitimately
        // need to override a generic "company" the upstream payload happens to carry.
        var cfg = JobnetLike() with
        {
            StaticFields = new Dictionary<string, string> { ["company"] = "Pleo" },
        };
        using var http = new HttpClient(new StubHandler(HappyPathPayload));
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Pleo", r.Company));
    }

    [Fact]
    public async Task FetchAsync_StaticFields_Empty_Value_Does_Not_Override()
    {
        var cfg = JobnetLike() with
        {
            StaticFields = new Dictionary<string, string> { ["company"] = "   " },
        };
        using var http = new HttpClient(new StubHandler(HappyPathPayload));
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();
        Assert.Equal("Contoso", results[0].Company);
    }

    [Fact]
    public async Task FetchAsync_Http_Error_Throws()
    {
        using var http = new HttpClient(new StubHandler("oops", HttpStatusCode.InternalServerError));
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.FetchAsync());
    }

    private static PortalConfig JoobleLike() => new(
        Name: "jooble",
        Type: PortalType.Api,
        Enabled: true,
        Method: "post",
        Endpoint: new Uri("https://example.com/api/{api_key}"),
        QueryParams: new Dictionary<string, object?>
        {
            ["api_key"] = "ABC123",
        },
        BodyTemplate: new Dictionary<string, object?>
        {
            ["keywords"] = "software",
            ["location"] = "Denmark",
            ["page"] = 1,
        },
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "jobs",
            ["id"] = "id",
            ["title"] = "title",
            ["url"] = "link",
        },
        RateLimitRps: 0);

    [Fact]
    public async Task FetchAsync_Post_Sends_Json_Body_From_BodyTemplate()
    {
        var handler = new CapturingHandler("""{"jobs":[]}""");
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(JoobleLike(), http, NullLogger.Instance);

        await adapter.FetchAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.NotNull(handler.LastBody);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("software", body.RootElement.GetProperty("keywords").GetString());
        Assert.Equal("Denmark", body.RootElement.GetProperty("location").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task FetchAsync_Post_Sets_ContentType_Application_Json()
    {
        var handler = new CapturingHandler("""{"jobs":[]}""");
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(JoobleLike(), http, NullLogger.Instance);

        await adapter.FetchAsync();

        Assert.Equal("application/json", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task FetchAsync_EndpointTemplate_Substitutes_From_QueryParams_And_Removes_Consumed_Key()
    {
        var handler = new CapturingHandler("""{"jobs":[]}""");
        using var http = new HttpClient(handler);
        var cfg = JoobleLike() with
        {
            QueryParams = new Dictionary<string, object?>
            {
                ["api_key"] = "ABC123",
                ["other"] = "preserved",
            },
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        await adapter.FetchAsync();

        var actualUri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("/api/ABC123", actualUri);
        Assert.DoesNotContain("api_key=", actualUri);
        Assert.Contains("other=preserved", actualUri);
    }

    [Fact]
    public async Task FetchAsync_EndpointTemplate_Unknown_Key_Throws_ConfigException()
    {
        var handler = new CapturingHandler("""{"jobs":[]}""");
        using var http = new HttpClient(handler);
        var cfg = JoobleLike() with
        {
            Endpoint = new Uri("https://example.com/api/{missing_key}"),
            QueryParams = new Dictionary<string, object?>
            {
                ["api_key"] = "ABC123",
            },
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => adapter.FetchAsync());
        Assert.Contains("missing_key", ex.Message);
    }

    [Fact]
    public async Task FetchAsync_Method_Defaults_To_Get_When_Unspecified()
    {
        var handler = new CapturingHandler(HappyPathPayload);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(JobnetLike(), http, NullLogger.Instance);

        await adapter.FetchAsync();

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Null(handler.LastRequest!.Content);
    }

    private static PortalConfig PaginatedGet() => new(
        Name: "paged-get",
        Type: PortalType.Api,
        Enabled: true,
        Endpoint: new Uri("https://example.com/api/jobs"),
        QueryParams: new Dictionary<string, object?>
        {
            ["q"] = "software",
        },
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "jobs",
            ["id"] = "id",
            ["title"] = "title",
            ["url"] = "link",
        },
        RateLimitRps: 0);

    [Fact]
    public async Task FetchAsync_Pagination_Stops_On_Empty_Page()
    {
        var full = """{"jobs":[{"id":"a","title":"A","link":"https://ex.com/a"}]}""";
        var empty = """{"jobs":[]}""";
        var handler = new ScriptedHandler(full, full, empty);
        using var http = new HttpClient(handler);
        var cfg = PaginatedGet() with
        {
            Pagination = new PaginationConfig(Param: "page", Start: 1, Step: 1, MaxPages: 10),
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("page=1", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.ToString());
        Assert.Contains("page=3", handler.Requests[2].RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchAsync_Pagination_Stops_On_Partial_Page()
    {
        var full = """{"jobs":[{"id":"a","title":"A","link":"https://ex.com/a"},{"id":"b","title":"B","link":"https://ex.com/b"}]}""";
        var partial = """{"jobs":[{"id":"c","title":"C","link":"https://ex.com/c"}]}""";
        var handler = new ScriptedHandler(full, partial);
        using var http = new HttpClient(handler);
        var cfg = PaginatedGet() with
        {
            Pagination = new PaginationConfig(Param: "page", Start: 1, Step: 1, Size: 2, MaxPages: 10),
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task FetchAsync_Pagination_Respects_MaxPages()
    {
        var full = """{"jobs":[{"id":"a","title":"A","link":"https://ex.com/a"}]}""";
        var handler = new ScriptedHandler(full, full);
        using var http = new HttpClient(handler);
        var cfg = PaginatedGet() with
        {
            Pagination = new PaginationConfig(Param: "page", Start: 1, Step: 1, MaxPages: 2),
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task FetchAsync_Honours_RateLimitRps_Across_Pages()
    {
        var full = """{"jobs":[{"id":"a","title":"A","link":"https://ex.com/a"}]}""";
        var empty = """{"jobs":[]}""";
        var handler = new ScriptedHandler(full, full, empty);
        using var http = new HttpClient(handler);
        var cfg = PaginatedGet() with
        {
            Pagination = new PaginationConfig(Param: "page", Start: 1, Step: 1, MaxPages: 10),
            RateLimitRps = 10.0,
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await adapter.FetchAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 180,
            $"expected >= 180ms (2 intervals at 10 rps), got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task FetchAsync_Pagination_Post_Injects_Into_Body_Not_Query()
    {
        var full = """{"jobs":[{"id":"a","title":"A","link":"https://ex.com/a"}]}""";
        var empty = """{"jobs":[]}""";
        var handler = new ScriptedHandler(full, empty);
        using var http = new HttpClient(handler);
        var cfg = JoobleLike() with
        {
            Pagination = new PaginationConfig(Param: "page", Start: 1, Step: 1, MaxPages: 10),
        };
        var adapter = new ApiAdapter(cfg, http, NullLogger.Instance);

        await adapter.FetchAsync();

        Assert.Equal(2, handler.Requests.Count);
        var body0 = JsonDocument.Parse(handler.RequestBodies[0]!);
        Assert.Equal(1, body0.RootElement.GetProperty("page").GetInt32());
        var body1 = JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.Equal(2, body1.RootElement.GetProperty("page").GetInt32());
        Assert.DoesNotContain("page=", handler.Requests[0].RequestUri!.ToString());
    }

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class CapturingHandler(string responseBody, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];

        public ScriptedHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (!_responses.TryDequeue(out var body))
            {
                throw new InvalidOperationException(
                    $"ScriptedHandler ran out of canned responses after {Requests.Count} call(s)");
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
