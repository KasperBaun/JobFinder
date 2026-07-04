using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Configuration;
using Jobmatch.IO;
using Jobmatch.Models;
using Jobmatch.Search;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Search;

public sealed class SearchServiceTests : IDisposable
{
    private static readonly IFileSystem Fs = new PhysicalFileSystem();
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public SearchServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-search-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private const string MinimalSkillset = """
        ---
        name: Test User
        location: Copenhagen, Denmark
        experience_years: 5
        target_roles:
          - Software Engineer
        remote_preference: remote
        seniority: mid
        languages:
          - English
        employment_types:
          - full-time
        ---

        ## Primary stack
        Must-have.

        - Python
        - TypeScript

        ## Secondary stack
        Nice-to-have.

        - Kubernetes

        ## Domains

        ## Disqualifiers
        """;

    private const string MinimalRanking = """
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

    private (JobmatchUserContext Ctx, IReadOnlyList<PortalConfig> Portals) CreateContext(
        string email, string portalsYaml, string? rankingYaml = null)
    {
        var ctx = JobmatchUserContext.Resolve(emailOverride: email, repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(ctx.SkillsetPath, MinimalSkillset);
        // Always use a local ranking.yml so we don't depend on AppContext.BaseDirectory being writable.
        File.WriteAllText(Path.Combine(ctx.RootDir, "ranking.yml"), rankingYaml ?? MinimalRanking);
        // Re-resolve so RankingPath now points at the user-local file.
        ctx = JobmatchUserContext.Resolve(emailOverride: email, repoRoot: _tempRoot, seedExamples: false);
        var portals = PortalConfigLoader.Parse(portalsYaml);
        return (ctx, portals);
    }

    private static async Task<List<SearchProgressEvent>> Drain(IAsyncEnumerable<SearchProgressEvent> stream)
    {
        var list = new List<SearchProgressEvent>();
        await foreach (var evt in stream) list.Add(evt);
        return list;
    }

    [Fact]
    public async Task RunAsync_Empty_Enabled_Yields_Started_Dedupe_Rank_Complete()
    {
        const string portals = """
            portals:
              - name: jobnet
                type: api
                enabled: false
                endpoint: https://job.jobnet.dk/CV/FindWork/Search
            """;
        var (ctx, portalList) = CreateContext("empty@example.com", portals);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));

        Assert.Equal(4, events.Count);
        var started = Assert.IsType<StartedEvent>(events[0]);
        Assert.Equal(0, started.Total);
        var dedupe = Assert.IsType<DedupeEvent>(events[1]);
        Assert.Equal(0, dedupe.MergedCount);
        var rank = Assert.IsType<RankEvent>(events[2]);
        Assert.Equal(0, rank.RankedCount);
        Assert.Equal(0.0, rank.TopScore);
        var complete = Assert.IsType<CompleteEvent>(events[3]);
        Assert.NotEmpty(complete.RunId);
        Assert.Empty(complete.Shortlist);
    }

    [Fact]
    public async Task RunAsync_Single_Manual_Provider_Streams_Per_Provider_Events()
    {
        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("manual@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-2026-04-20.json"),
            """
            [
              {
                "title": "Senior Python Engineer",
                "company": "Acme",
                "location": "Copenhagen",
                "url": "https://acme.com/jobs/1",
                "description": "Python and TypeScript stack. Fully remote.",
                "posted_at": "2026-05-01T09:00:00Z"
              }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));

        Assert.IsType<StartedEvent>(events[0]);
        var running = Assert.IsType<ProviderRunningEvent>(events[1]);
        Assert.Equal("mine", running.Provider);
        Assert.Equal(1, running.Index);
        Assert.Equal(1, running.Total);
        var done = Assert.IsType<ProviderDoneEvent>(events[2]);
        Assert.Equal("mine", done.Provider);
        Assert.Equal(1, done.FetchedCount);
        Assert.IsType<DedupeEvent>(events[3]);
        var rank = Assert.IsType<RankEvent>(events[4]);
        Assert.Equal(1, rank.RankedCount);
        Assert.True(rank.TopScore > 0);
        var complete = Assert.IsType<CompleteEvent>(events[5]);
        Assert.Single(complete.Shortlist);
        Assert.Equal("Senior Python Engineer", complete.Shortlist[0].Title);
        Assert.Contains("Python", complete.Shortlist[0].PrimaryStackHits);
    }

    [Fact]
    public async Task RunAsync_Mixed_Success_And_Failure_Records_Both_Statuses()
    {
        // Two manual providers — the second uses a name that won't match any imports file,
        // but a manual provider with no files just returns 0 (it doesn't fail). Force a real
        // failure by making one provider an api type with an unreachable endpoint.
        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
              - name: broken
                type: api
                enabled: true
                endpoint: http://127.0.0.1:1/unreachable
                response_mapping:
                  items_path: "data"
                  id: "id"
                  title: "title"
                  url_template: "http://x/{id}"
            """;
        var (ctx, portalList) = CreateContext("mixed@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-x.json"),
            """
            [
              {
                "title": "Backend Engineer",
                "company": "Acme",
                "location": "Copenhagen",
                "url": "https://acme.com/jobs/2",
                "description": "Python role.",
                "posted_at": "2026-04-20T09:00:00Z"
              }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));

        var started = Assert.IsType<StartedEvent>(events[0]);
        Assert.Equal(2, started.Total);

        var hasOk = events.OfType<ProviderDoneEvent>().Any(e => e.Provider == "mine");
        var hasFailed = events.OfType<ProviderFailedEvent>().Any(e => e.Provider == "broken");
        Assert.True(hasOk, "expected provider_done for 'mine'");
        Assert.True(hasFailed, "expected provider_failed for 'broken'");

        var complete = Assert.IsType<CompleteEvent>(events[^1]);
        Assert.Single(complete.Shortlist);
    }

    [Fact]
    public async Task RunAsync_Writes_History_File_With_RunId()
    {
        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("history@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"),
            """
            [
              { "title": "Python Dev", "company": "X", "url": "https://x.com/1", "description": "Python.", "location": "Copenhagen" }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));
        var complete = Assert.IsType<CompleteEvent>(events[^1]);

        var historyFile = Path.Combine(ctx.HistoryDir, $"{complete.RunId}.json");
        Assert.True(File.Exists(historyFile), $"expected history file at {historyFile}");

        var json = File.ReadAllText(historyFile);
        Assert.Contains("\"runId\"", json);
        Assert.Contains(complete.RunId, json);
        Assert.Contains("\"providers\"", json);
        Assert.Contains("\"shortlist\"", json);
    }

    [Fact]
    public async Task RunAsync_Does_Not_Touch_MarksFile()
    {
        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("marks-untouched@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"),
            """
            [
              { "title": "Python Dev", "company": "X", "url": "https://x.com/1", "description": "Python.", "location": "Copenhagen" }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        await Drain(service.RunAsync(new SearchRequest(), portalList));

        Assert.False(File.Exists(ctx.MarksPath), "search should not create marks.json");
    }

    [Fact]
    public async Task RunAsync_Persists_Transparency_Sections_With_Correct_Drop_Reasons()
    {
        // Skillset with explicit disqualifier so we can trigger that reason.
        const string skillset = """
            ---
            name: Test User
            location: Copenhagen, Denmark
            experience_years: 5
            target_roles:
              - Software Engineer
            remote_preference: remote
            seniority: mid
            languages:
              - English
            employment_types:
              - full-time
            ---

            ## Primary stack
            - Python
            - TypeScript

            ## Secondary stack
            - Kubernetes

            ## Domains

            ## Disqualifiers
            - unpaid
            """;

        // min_score 0.10 lets a strong match through but rejects weak hits.
        // disqualifier_penalty 0.0 zeroes anything with a disqualifier word.
        const string ranking = """
            weights:
              primary_stack: 0.6
              secondary_stack: 0.1
              seniority: 0.1
              location_remote: 0.1
              domain: 0.05
              freshness: 0.05

            disqualifier_penalty: 0.0
            top_n: 10
            freshness_half_life_days: 14
            min_score_to_include: 0.25
            require_primary_stack_hit: false
            """;

        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("transparency@example.com", portals, rankingYaml: ranking);
        File.WriteAllText(ctx.SkillsetPath, skillset);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"),
            """
            [
              {
                "title": "Senior Python Engineer",
                "company": "Acme",
                "location": "Copenhagen",
                "url": "https://acme.com/jobs/strong",
                "description": "Python and TypeScript stack.",
                "posted_at": "2026-05-01T09:00:00Z"
              },
              {
                "title": "Marketing Manager",
                "company": "Brandly",
                "location": "Copenhagen",
                "url": "https://brandly.com/jobs/m",
                "description": "Run our paid campaigns. No technical work.",
                "posted_at": "2026-05-01T09:00:00Z"
              },
              {
                "title": "Unpaid Python Developer",
                "company": "Volunteer Co",
                "location": "Copenhagen",
                "url": "https://volunteer.com/jobs/x",
                "description": "Python stack — volunteer-friendly role.",
                "posted_at": "2026-05-01T09:00:00Z"
              }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));
        var complete = Assert.IsType<CompleteEvent>(events[^1]);

        var historyJson = File.ReadAllText(Path.Combine(ctx.HistoryDir, $"{complete.RunId}.json"));
        var detail = JsonSerializer.Deserialize<RunDetail>(historyJson, HistoryReadOptions);

        Assert.NotNull(detail);
        Assert.NotNull(detail!.Raw);
        Assert.NotNull(detail.Scored);
        Assert.NotNull(detail.Dropped);

        // Raw section: one provider with three listings.
        Assert.Single(detail.Raw!);
        Assert.Equal("mine", detail.Raw[0].Provider);
        Assert.Equal(3, detail.Raw[0].Listings.Count);

        // Scored section: all three deduped listings get a breakdown.
        Assert.Equal(3, detail.Scored!.Count);
        Assert.All(detail.Scored, s => Assert.NotNull(s.Breakdown));

        // Shortlist gets the strong Python match.
        Assert.Single(detail.Shortlist);
        Assert.Equal("Senior Python Engineer", detail.Shortlist[0].Title);

        // Dropped section: marketing role below threshold; volunteer role hit disqualifier.
        Assert.Equal(2, detail.Dropped!.Count);
        var droppedByReason = detail.Dropped.GroupBy(d => d.Reason).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Single(droppedByReason["disqualifier"]);
        Assert.Equal("Unpaid Python Developer", droppedByReason["disqualifier"][0].Title);
        Assert.Single(droppedByReason["below_min_score"]);
        Assert.Equal("Marketing Manager", droppedByReason["below_min_score"][0].Title);
        Assert.Contains("unpaid", droppedByReason["disqualifier"][0].Context);
    }

    [Fact]
    public async Task RunAsync_BeyondTopN_Records_Dropped_Reason()
    {
        // top_n=1 with two strong-scoring matches — the lower-scored one gets a beyond_top_n drop.
        const string ranking = """
            weights:
              primary_stack: 0.7
              secondary_stack: 0.1
              seniority: 0.1
              location_remote: 0.05
              domain: 0.025
              freshness: 0.025

            disqualifier_penalty: 0.0
            top_n: 1
            freshness_half_life_days: 14
            min_score_to_include: 0.0
            require_primary_stack_hit: false
            """;

        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("beyond@example.com", portals, rankingYaml: ranking);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"),
            """
            [
              { "title": "Strong Python Role", "url": "https://x.com/1", "description": "Python TypeScript.", "location": "Copenhagen" },
              { "title": "Decent Python Role", "url": "https://x.com/2", "description": "Python.", "location": "Copenhagen" }
            ]
            """);

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(), portalList));
        var complete = Assert.IsType<CompleteEvent>(events[^1]);

        var detail = JsonSerializer.Deserialize<RunDetail>(
            File.ReadAllText(Path.Combine(ctx.HistoryDir, $"{complete.RunId}.json")),
            HistoryReadOptions);
        Assert.NotNull(detail);

        Assert.Single(detail!.Shortlist);
        var beyond = detail.Dropped!.Where(d => d.Reason == "beyond_top_n").ToList();
        Assert.Single(beyond);
        Assert.Contains("rank 2 of 2", beyond[0].Context);
    }

    [Fact]
    public async Task History_ScoredEntries_CarryStackHits()
    {
        const string portals = """
            portals:
              - name: stack-test
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("stack-hits@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "stack-test-1.json"),
            """
            [
              {
                "title": "Senior Python Engineer",
                "company": "Acme",
                "location": "Remote",
                "url": "https://acme.com/jobs/1",
                "description": "We use Python and Kubernetes",
                "posted_at": "2026-05-01T09:00:00Z"
              }
            ]
            """);

        var svc = new SearchService(ctx, Fs);
        var events = await Drain(svc.RunAsync(new SearchRequest(), portalList));
        var complete = Assert.IsType<CompleteEvent>(events[^1]);

        var historyFile = Path.Combine(ctx.HistoryDir, $"{complete.RunId}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(historyFile));
        var scored = doc.RootElement.GetProperty("scored")[0];
        var primary = scored.GetProperty("primaryStackHits").EnumerateArray().Select(e => e.GetString()).ToArray();
        var secondary = scored.GetProperty("secondaryStackHits").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("Python", primary);
        Assert.Contains("Kubernetes", secondary);
    }

    private static readonly JsonSerializerOptions HistoryReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task RunAsync_Provider_Filter_Limits_Run_To_Named_Portals()
    {
        const string portals = """
            portals:
              - name: mine
                type: manual
                enabled: true
              - name: other
                type: manual
                enabled: true
            """;
        var (ctx, portalList) = CreateContext("filter@example.com", portals);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-1.json"),
            """[ { "title": "X", "url": "https://x.com/1" } ]""");
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "other-1.json"),
            """[ { "title": "Y", "url": "https://y.com/1" } ]""");

        var service = new SearchService(ctx, Fs);
        var events = await Drain(service.RunAsync(new SearchRequest(Providers: ["mine"]), portalList));

        var started = Assert.IsType<StartedEvent>(events[0]);
        Assert.Equal(1, started.Total);
        Assert.Single(events.OfType<ProviderRunningEvent>());
        Assert.Equal("mine", events.OfType<ProviderRunningEvent>().Single().Provider);
    }
}
