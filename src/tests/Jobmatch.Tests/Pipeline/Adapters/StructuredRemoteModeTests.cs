using System.Net;
using System.Text.Json;
using System.Text;
using Jobmatch.Domain;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Fetching.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Pipeline.Adapters;

// Structured remote-mode extraction: when the source states the arrangement as a field, that value
// wins over InferRemoteMode. Payload shapes below are trimmed from live responses captured
// 2026-08-06 (Oracle Recruiting Cloud, SmartRecruiters, Ashby, Lever).
public sealed class StructuredRemoteModeTests
{
    [Theory]
    [InlineData("ORA_REMOTE", RemoteMode.Remote)]
    [InlineData("ORA_HYBRID", RemoteMode.Hybrid)]
    [InlineData("ORA_ON_SITE", RemoteMode.Onsite)]
    [InlineData("On-site", RemoteMode.Onsite)]
    [InlineData("Remote", RemoteMode.Remote)]
    [InlineData("Hybrid", RemoteMode.Hybrid)]
    [InlineData("onsite", RemoteMode.Onsite)]
    [InlineData("Site Based", RemoteMode.Onsite)]
    [InlineData("Fully Remote", RemoteMode.Remote)]
    public void MapWorkplaceToken_ReadsEveryVendorSpelling(string token, RemoteMode expected)
    {
        Assert.Equal(expected, BaseAdapter.MapWorkplaceToken(token));
    }

    // Each vendor has a value meaning "nothing stated". Mapping those to a mode would claim an
    // arrangement the employer never gave, so they stay silent and inference takes over.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Flexible")]
    [InlineData("unspecified")]
    [InlineData("Partially remote")]
    public void MapWorkplaceToken_LeavesAmbiguousValuesToInference(string? token)
    {
        Assert.Null(BaseAdapter.MapWorkplaceToken(token));
    }

    [Fact]
    public void TryReadStructuredRemoteMode_NonObjectPayload_IsSilent()
    {
        Assert.Null(BaseAdapter.TryReadStructuredRemoteMode(Raw("{}")));
        Assert.Null(BaseAdapter.TryReadStructuredRemoteMode(Raw("[]")));
        Assert.Null(BaseAdapter.TryReadStructuredRemoteMode(Raw("""{"WorkplaceTypeCode": null}""")));
    }

    // Oracle publishes both a stable code and a localisable label; the code is authoritative.
    [Fact]
    public void TryReadStructuredRemoteMode_Oracle_PrefersCodeOverLabel()
    {
        var raw = Raw("""{"WorkplaceTypeCode": "ORA_ON_SITE", "WorkplaceType": "Hybrid"}""");

        Assert.Equal(RemoteMode.Onsite, BaseAdapter.TryReadStructuredRemoteMode(raw));
    }

    [Fact]
    public void TryReadStructuredRemoteMode_Oracle_FallsBackToLabelWhenCodeIsBlank()
    {
        var raw = Raw("""{"WorkplaceTypeCode": "", "WorkplaceType": "Remote"}""");

        Assert.Equal(RemoteMode.Remote, BaseAdapter.TryReadStructuredRemoteMode(raw));
    }

    [Theory]
    [InlineData("""{"location": {"city": "Aarhus", "remote": false, "hybrid": true}}""", RemoteMode.Hybrid)]
    [InlineData("""{"location": {"city": "Aarhus", "remote": true, "hybrid": false}}""", RemoteMode.Remote)]
    public void TryReadStructuredRemoteMode_SmartRecruiters_ReadsASetFlag(string json, RemoteMode expected)
    {
        Assert.Equal(expected, BaseAdapter.TryReadStructuredRemoteMode(Raw(json)));
    }

    // SmartRecruiters' two booleans have no "unset" state, and four of the five DK employers we poll
    // leave both false on every posting — so false is the editor's default, not a claim of onsite.
    [Fact]
    public void TryReadStructuredRemoteMode_SmartRecruiters_BothFalse_IsSilentNotOnsite()
    {
        var raw = Raw("""{"location": {"city": "Copenhagen", "remote": false, "hybrid": false}}""");

        Assert.Null(BaseAdapter.TryReadStructuredRemoteMode(raw));
    }

    // Ashby sets isRemote on every posting it publishes, including the ones whose workplaceType is
    // Hybrid, so the boolean means "not strictly onsite" and only workplaceType is evidence.
    [Fact]
    public void TryReadStructuredRemoteMode_Ashby_TrustsWorkplaceTypeNotIsRemote()
    {
        var raw = Raw("""{"workplaceType": "Hybrid", "isRemote": true, "location": "London"}""");

        Assert.Equal(RemoteMode.Hybrid, BaseAdapter.TryReadStructuredRemoteMode(raw));
    }

    [Fact]
    public void TryReadStructuredRemoteMode_Lever_ReadsLowercaseWorkplaceType()
    {
        var raw = Raw("""{"workplaceType": "remote", "text": "Staff Engineer"}""");

        Assert.Equal(RemoteMode.Remote, BaseAdapter.TryReadStructuredRemoteMode(raw));
    }

    [Fact]
    public async Task FetchAsync_Oracle_StatedWorkplaceType_ReplacesUnknown()
    {
        using var http = new HttpClient(new StubHandler(OraclePayload));
        var adapter = new ApiAdapter(OracleLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Hybrid, results[0].RemoteMode);
        Assert.Equal(RemoteMode.Onsite, results[1].RemoteMode);
        Assert.Equal(RemoteMode.Unknown, results[2].RemoteMode);
    }

    // The structured field is the employer's own answer, so it wins even when the ad text reads the
    // other way — here a title cue that InferRemoteMode alone would have scored as fully remote.
    [Fact]
    public async Task FetchAsync_Oracle_StatedOnsite_OutranksARemoteCueInTheTitle()
    {
        const string payload = """
            {
              "items": [{ "requisitionList": [{
                "Id": "9",
                "Title": "Support Engineer - Remote",
                "PrimaryLocation": "Bengaluru, India",
                "ShortDescriptionStr": "Join the platform team.",
                "WorkplaceTypeCode": "ORA_ON_SITE",
                "WorkplaceType": "On-site"
              }] }]
            }
            """;
        using var http = new HttpClient(new StubHandler(payload));
        var adapter = new ApiAdapter(OracleLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Remote, BaseAdapter.InferRemoteMode("Support Engineer - Remote", "Bengaluru, India", ""));
        Assert.Equal(RemoteMode.Onsite, Assert.Single(results).RemoteMode);
    }

    [Fact]
    public async Task FetchAsync_SmartRecruiters_HybridFlag_ReplacesUnknown_BothFalseInfers()
    {
        using var http = new HttpClient(new StubHandler(SmartRecruitersPayload));
        var adapter = new ApiAdapter(SmartRecruitersLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        Assert.Equal(RemoteMode.Hybrid, results[0].RemoteMode);
        Assert.Equal(RemoteMode.Unknown, results[1].RemoteMode);
        // Both flags false, but the title says hybrid — inference still gets its turn.
        Assert.Equal(RemoteMode.Hybrid, results[2].RemoteMode);
    }

    private const string OraclePayload = """
        {
          "items": [{ "requisitionList": [
            {
              "Id": "23956",
              "Title": "Experienced Compliance Officer - Markets",
              "PrimaryLocation": "STOCKHOLM, Sweden",
              "ShortDescriptionStr": "Advise the Markets business.",
              "WorkplaceTypeCode": "ORA_HYBRID",
              "WorkplaceType": "Hybrid"
            },
            {
              "Id": "23957",
              "Title": "Branch Advisor",
              "PrimaryLocation": "AARHUS, Denmark",
              "ShortDescriptionStr": "Serve customers in branch.",
              "WorkplaceTypeCode": "ORA_ON_SITE",
              "WorkplaceType": "On-site"
            },
            {
              "Id": "23958",
              "Title": "Data Engineer",
              "PrimaryLocation": "COPENHAGEN, Denmark",
              "ShortDescriptionStr": "Build the data platform.",
              "WorkplaceTypeCode": null,
              "WorkplaceType": ""
            }
          ] }]
        }
        """;

    private const string SmartRecruitersPayload = """
        {
          "content": [
            {
              "id": "a1",
              "name": "Senior AI Engineer",
              "releasedDate": "2026-07-01T10:00:00.000Z",
              "location": { "city": "København", "country": "dk", "remote": false, "hybrid": true, "fullLocation": "København, , Denmark" }
            },
            {
              "id": "a2",
              "name": "Student Assistant, IT Security",
              "releasedDate": "2026-07-02T10:00:00.000Z",
              "location": { "city": "Copenhagen", "country": "dk", "remote": false, "hybrid": false, "fullLocation": "Copenhagen, , Denmark" }
            },
            {
              "id": "a3",
              "name": "Softwareudvikler",
              "releasedDate": "2026-07-03T10:00:00.000Z",
              "location": { "city": "Aarhus, hybrid position", "country": "dk", "remote": false, "hybrid": false, "fullLocation": "Aarhus, hybrid position" }
            }
          ]
        }
        """;

    private static PortalConfig OracleLike() => new(
        Name: "oracle-danskebank",
        Type: PortalType.Api,
        Enabled: true,
        Endpoint: new Uri("https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions"),
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "items.0.requisitionList",
            ["id"] = "Id",
            ["title"] = "Title",
            ["location"] = "PrimaryLocation",
            ["description"] = "ShortDescriptionStr",
            ["url_template"] = "https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/job/{Id}",
        },
        StaticFields: new Dictionary<string, string> { ["company"] = "Danske Bank" },
        RateLimitRps: 0);

    private static PortalConfig SmartRecruitersLike() => new(
        Name: "smartrecruiters-devoteam",
        Type: PortalType.Api,
        Enabled: true,
        Endpoint: new Uri("https://api.smartrecruiters.com/v1/companies/Devoteam/postings"),
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "content",
            ["id"] = "id",
            ["title"] = "name",
            ["location"] = "location.fullLocation",
            ["url_template"] = "https://jobs.smartrecruiters.com/Devoteam/{id}",
            ["posted_at"] = "releasedDate",
        },
        StaticFields: new Dictionary<string, string> { ["company"] = "Devoteam" },
        RateLimitRps: 0);

    private static JsonElement Raw(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
