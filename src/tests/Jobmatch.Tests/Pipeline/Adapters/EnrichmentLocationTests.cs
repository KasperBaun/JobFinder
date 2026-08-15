using System.Net;
using System.Text;
using Jobmatch.Features.Providers;
using Jobmatch.Search.Fetching.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Pipeline.Adapters;

// Location repair during body enrichment: Workday CXS detail lookup (all locations for
// "N Locations" placeholders) and the generic JSON-LD JobPosting fallback.
public sealed class EnrichmentLocationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("2 Locations", true)]
    [InlineData("10 locations", true)]
    [InlineData("1 Location", true)]
    [InlineData("Copenhagen", false)]
    [InlineData("Headquarters (IT)", false)]
    public void IsMissingOrPlaceholderLocation_FlagsNullBlankAndCountPlaceholders(string? location, bool expected)
    {
        Assert.Equal(expected, BaseAdapter.IsMissingOrPlaceholderLocation(location));
    }

    [Theory]
    [InlineData(
        "https://simcorp.wd3.myworkdayjobs.com/en-US/SimCorp_Jobs/job/Noida/Lead-Software-Engineer_R-209673-1",
        "https://simcorp.wd3.myworkdayjobs.com/wday/cxs/simcorp/SimCorp_Jobs/job/Noida/Lead-Software-Engineer_R-209673-1")]
    [InlineData(
        "https://lego.wd103.myworkdayjobs.com/LEGO_External/job/Billund/Senior-Engineer_R123",
        "https://lego.wd103.myworkdayjobs.com/wday/cxs/lego/LEGO_External/job/Billund/Senior-Engineer_R123")]
    public void TryBuildWorkdayCxsUrl_RewritesJobPageToCxsDetail(string listingUrl, string expected)
    {
        Assert.Equal(new Uri(expected), BaseAdapter.TryBuildWorkdayCxsUrl(new Uri(listingUrl)));
    }

    [Theory]
    [InlineData("https://example.com/job/Copenhagen/Engineer_1")]
    [InlineData("https://simcorp.wd3.myworkdayjobs.com/wday/cxs/simcorp/SimCorp_Jobs/job/Noida/X_R-1")]
    [InlineData("https://simcorp.wd3.myworkdayjobs.com/en-US/SimCorp_Jobs/jobs")]
    public void TryBuildWorkdayCxsUrl_LeavesNonJobPagesAlone(string listingUrl)
    {
        Assert.Null(BaseAdapter.TryBuildWorkdayCxsUrl(new Uri(listingUrl)));
    }

    [Fact]
    public void ParseWorkdayCxs_ReadsDescriptionAndEveryLocation()
    {
        const string json = """
            {
              "jobPostingInfo": {
                "jobDescription": "<p>Build the platform.</p>",
                "location": "Noida",
                "additionalLocations": ["Warsaw", "Noida"]
              }
            }
            """;

        var posting = BaseAdapter.ParseWorkdayCxs(json);

        Assert.NotNull(posting);
        Assert.Equal("<p>Build the platform.</p>", posting.DescriptionHtml);
        Assert.Equal(["Noida", "Warsaw"], posting.Locations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"unexpected\": true}")]
    public void ParseWorkdayCxs_ToleratesMissingOrMalformedPayloads(string? json)
    {
        Assert.Null(BaseAdapter.ParseWorkdayCxs(json));
    }

    [Fact]
    public void ExtractJsonLdLocation_ReadsJobPostingPlace()
    {
        const string html = """
            <html><head><script type="application/ld+json">
            {"@type":"JobPosting","title":"Engineer",
             "jobLocation":{"@type":"Place","address":{"@type":"PostalAddress","addressCountry":"India","addressLocality":"Noida"}}}
            </script></head><body/></html>
            """;

        Assert.Equal("Noida, India", BaseAdapter.ExtractJsonLdLocation(html));
    }

    [Fact]
    public void ExtractJsonLdLocation_JoinsMultiplePlacesWithGazetteerSeparator()
    {
        const string html = """
            <script type="application/ld+json">
            {"@type":"JobPosting","jobLocation":[
              {"@type":"Place","address":{"addressLocality":"Copenhagen","addressCountry":"Denmark"}},
              {"@type":"Place","address":{"addressLocality":"Warsaw","addressCountry":"Poland"}}]}
            </script>
            """;

        Assert.Equal("Copenhagen, Denmark / Warsaw, Poland", BaseAdapter.ExtractJsonLdLocation(html));
    }

    [Fact]
    public void ExtractJsonLdLocation_SkipsMalformedAndNonJobPostingBlocks()
    {
        const string html = """
            <script type="application/ld+json">{not json</script>
            <script type="application/ld+json">{"@type":"Organization","name":"Acme"}</script>
            <script type="application/ld+json">{"@type":"JobPosting","jobLocation":{"address":{"addressLocality":"Aarhus"}}}</script>
            """;

        Assert.Equal("Aarhus", BaseAdapter.ExtractJsonLdLocation(html));
    }

    [Fact]
    public void ExtractJsonLdLocation_NoJobPosting_ReturnsNull()
    {
        Assert.Null(BaseAdapter.ExtractJsonLdLocation("<html><body>plain</body></html>"));
        Assert.Null(BaseAdapter.ExtractJsonLdLocation(null));
    }

    [Fact]
    public async Task EnrichBodies_Workday_ReplacesPlaceholderLocation_AndUsesCxsDescription()
    {
        const string listPage = """
            {
              "total": 1,
              "jobPostings": [
                {
                  "title": "Lead Software Engineer",
                  "externalPath": "/job/Noida/Lead-Software-Engineer_R-209673-1",
                  "locationsText": "2 Locations",
                  "postedOn": "Posted Today"
                }
              ]
            }
            """;
        const string cxsDetail = """
            {
              "jobPostingInfo": {
                "jobDescription": "<p>Own the .NET platform in Noida or Warsaw.</p>",
                "location": "Noida",
                "additionalLocations": ["Warsaw"]
              }
            }
            """;
        var handler = new ScriptedHandler(listPage, cxsDetail);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(SimcorpLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        var listing = Assert.Single(results);
        Assert.Equal("Noida / Warsaw", listing.Location);
        Assert.Contains("Own the .NET platform", listing.Description);
    }

    [Fact]
    public async Task EnrichBodies_Workday_KeepsMappedSingleLocation()
    {
        const string listPage = """
            {
              "total": 1,
              "jobPostings": [
                {
                  "title": "Senior Software Engineer",
                  "externalPath": "/job/Copenhagen/Senior-Software-Engineer_R-1",
                  "locationsText": "Copenhagen",
                  "postedOn": "Posted Today"
                }
              ]
            }
            """;
        const string cxsDetail = """
            {
              "jobPostingInfo": {
                "jobDescription": "<p>Copenhagen HQ role.</p>",
                "location": "Copenhagen"
              }
            }
            """;
        var handler = new ScriptedHandler(listPage, cxsDetail);
        using var http = new HttpClient(handler);
        var adapter = new ApiAdapter(SimcorpLike(), http, NullLogger.Instance);

        var results = await adapter.FetchAsync();

        var listing = Assert.Single(results);
        Assert.Equal("Copenhagen", listing.Location);
        Assert.Contains("Copenhagen HQ role", listing.Description);
    }

    private static PortalConfig SimcorpLike() => new(
        Name: "workday-simcorp",
        Type: PortalType.Api,
        Enabled: true,
        Method: "post",
        Endpoint: new Uri("https://simcorp.wd3.myworkdayjobs.com/wday/cxs/simcorp/SimCorp_Jobs/jobs"),
        BodyTemplate: new Dictionary<string, object?> { ["searchText"] = "developer", ["limit"] = 20, ["offset"] = 0 },
        Pagination: null,
        ResponseMapping: new Dictionary<string, string>
        {
            ["items_path"] = "jobPostings",
            ["id"] = "externalPath",
            ["title"] = "title",
            ["location"] = "locationsText",
            ["url_template"] = "https://simcorp.wd3.myworkdayjobs.com/en-US/SimCorp_Jobs{externalPath}",
            ["posted_at"] = "postedOn",
        },
        StaticFields: new Dictionary<string, string> { ["company"] = "SimCorp" },
        RateLimitRps: 0,
        EnrichBody: true);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public ScriptedHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

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
