using Jobmatch.Features.Providers.Legacy;
using System.Text.Json;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Providers;
using Jobmatch.Search;
using Jobmatch.Infrastructure.IO;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Pipeline;

/// <summary>End-to-end radius filter behaviour (R-105) through the SearchService pipeline,
/// resolving against the real bundled gazetteer.</summary>
public sealed class SearchRunnerRadiusTests : IDisposable
{
    private static readonly IFileSystem Fs = new PhysicalFileSystem();
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public SearchRunnerRadiusTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-radius-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Home in central Copenhagen, 50 km radius.
    private const string RadiusSkillset = """
        ---
        name: Test User
        location: Copenhagen, Denmark
        experience_years: 5
        target_roles:
          - Software Engineer
        remote_preference: any
        seniority: mid
        languages:
          - English
        employment_types:
          - full-time
        address: Somewhere 1, 2300 København S
        radius_km: 50
        latitude: 55.6761
        longitude: 12.5683
        ---

        ## Primary stack
        - Python

        ## Secondary stack

        ## Domains

        ## Disqualifiers
        """;

    private const string PermissiveRanking = """
        weights:
          primary_stack: 0.5
          secondary_stack: 0.1
          seniority: 0.1
          location_remote: 0.2
          domain: 0.05
          freshness: 0.05

        disqualifier_penalty: 0.0
        top_n: 10
        freshness_half_life_days: 14
        min_score_to_include: 0.0
        require_primary_stack_hit: false
        """;

    private const string ManualPortal = """
        portals:
          - name: mine
            type: manual
            enabled: true
        """;

    private async Task<RunDetail> RunWith(string listingsJson, string? rankingYaml = null)
    {
        var ctx = JobmatchUserContext.Resolve(emailOverride: "radius@example.com", repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(ctx.SkillsetPath, RadiusSkillset);
        File.WriteAllText(Path.Combine(ctx.RootDir, "ranking.yml"), rankingYaml ?? PermissiveRanking);
        ctx = JobmatchUserContext.Resolve(emailOverride: "radius@example.com", repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"), listingsJson);

        var service = new SearchRunner(ctx, TestServices.Catalog(ctx), TestServices.Runs(ctx), Fs);
        var events = new List<SearchProgressEvent>();
        await foreach (var evt in service.RunAsync(new SearchRequest(), PortalsYamlLoader.Parse(ManualPortal)))
            events.Add(evt);
        var complete = Assert.IsType<CompleteEvent>(events[^1]);

        var detail = JsonSerializer.Deserialize<RunDetail>(
            File.ReadAllText(Path.Combine(ctx.HistoryDir, $"{complete.RunId}.json")),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            });
        Assert.NotNull(detail);
        return detail!;
    }

    [Fact]
    public async Task Far_Listing_Drops_With_OutsideRadius_And_Args()
    {
        var detail = await RunWith("""
            [
              { "title": "Python Engineer", "url": "https://x.com/1", "description": "Python.", "location": "Warsaw, Poland", "posted_at": "2026-07-20T09:00:00Z" }
            ]
            """);

        Assert.Empty(detail.Shortlist);
        var dropped = Assert.Single(detail.Dropped!);
        Assert.Equal("outside_radius", dropped.Reason);
        Assert.Contains("km away (Warsaw)", dropped.Context);
        Assert.NotNull(dropped.ContextArgs);
        Assert.Equal(50, ((JsonElement)dropped.ContextArgs!["maxKm"]).GetInt32());
        Assert.InRange(((JsonElement)dropped.ContextArgs["km"]).GetInt32(), 660, 680);
        Assert.Equal("Warsaw", ((JsonElement)dropped.ContextArgs["place"]).GetString());
    }

    [Fact]
    public async Task Remote_Far_Listing_Is_Exempt_And_Shortlisted()
    {
        var detail = await RunWith("""
            [
              { "title": "Python Engineer", "url": "https://x.com/1", "description": "Python. Fully remote role.", "location": "Warsaw, Poland", "posted_at": "2026-07-20T09:00:00Z" }
            ]
            """);

        Assert.Single(detail.Shortlist);
        Assert.Empty(detail.Dropped!);
    }

    [Fact]
    public async Task Listing_Without_Location_Passes()
    {
        var detail = await RunWith("""
            [
              { "title": "Python Engineer", "url": "https://x.com/1", "description": "Python.", "posted_at": "2026-07-20T09:00:00Z" }
            ]
            """);

        Assert.Single(detail.Shortlist);
    }

    [Fact]
    public async Task Nearby_Listing_Passes()
    {
        var detail = await RunWith("""
            [
              { "title": "Python Engineer", "url": "https://x.com/1", "description": "Python.", "location": "København", "posted_at": "2026-07-20T09:00:00Z" }
            ]
            """);

        Assert.Single(detail.Shortlist);
    }

    [Fact]
    public async Task Old_And_Far_Listing_Drops_As_AboveMaxAge_First()
    {
        var ranking = PermissiveRanking + "\nmax_age_days: 30\n";
        var detail = await RunWith("""
            [
              { "title": "Python Engineer", "url": "https://x.com/1", "description": "Python.", "location": "Warsaw, Poland", "posted_at": "2020-01-01T09:00:00Z" }
            ]
            """, ranking);

        var dropped = Assert.Single(detail.Dropped!);
        Assert.Equal("above_max_age", dropped.Reason);
    }
}
