using Jobmatch.Features.Providers.Legacy;
using System.Text.Json;
using Jobmatch.Domain.Runs;
using Jobmatch.Features.Providers;
using Jobmatch.Search;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Json;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Pipeline;

/// <summary>
/// The shortlist wire shape now carries the full fetched ad text (T-009), persisted per run so a
/// listing can be saved as PDF later. The field is additive and trailing: runs recorded before it
/// existed must still load, and runs whose sources have no body must not gain an empty field.
/// </summary>
public sealed class ListingMatchTests : IDisposable
{
    private static readonly IFileSystem Fs = new PhysicalFileSystem();
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public ListingMatchTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "jobmatch-listingmatch-tests-" + Guid.NewGuid().ToString("N"));
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

    private async Task<ListingMatch> RunSingleManualListing(string email, string listingJson)
    {
        var ctx = JobmatchUserContext.Resolve(emailOverride: email, repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(ctx.SkillsetPath, MinimalSkillset);
        File.WriteAllText(Path.Combine(ctx.RootDir, "ranking.yml"), MinimalRanking);
        ctx = JobmatchUserContext.Resolve(emailOverride: email, repoRoot: _tempRoot, seedExamples: false);
        var portals = PortalsYamlLoader.Parse("""
            portals:
              - name: mine
                type: manual
                enabled: true
            """);
        File.WriteAllText(Path.Combine(ctx.ImportsDir, "mine-2026-04-20.json"), $"[{listingJson}]");

        var service = new SearchRunner(ctx, TestServices.Catalog(ctx), TestServices.Runs(ctx), Fs);
        CompleteEvent? complete = null;
        await foreach (var evt in service.RunAsync(new SearchRequest(), portals))
        {
            if (evt is CompleteEvent c) complete = c;
        }

        Assert.NotNull(complete);
        return Assert.Single(complete.Shortlist);
    }

    [Fact]
    public async Task Shortlist_Entry_Carries_The_Fetched_Ad_Text()
    {
        var match = await RunSingleManualListing("desc@example.com", """
            {
              "title": "Senior Python Engineer",
              "company": "Acme",
              "url": "https://acme.com/jobs/1",
              "description": "Python and TypeScript stack.\nFully remote."
            }
            """);

        Assert.Equal("Python and TypeScript stack.\nFully remote.", match.Description);
    }

    [Fact]
    public async Task Bodyless_Listing_Yields_Null_Description_Not_Empty()
    {
        var match = await RunSingleManualListing("nodesc@example.com", """
            {
              "title": "Senior Python Engineer",
              "company": "Acme",
              "url": "https://acme.com/jobs/1"
            }
            """);

        Assert.Null(match.Description);
        var json = JsonSerializer.Serialize(match, JobmatchJsonOptions.Default);
        Assert.DoesNotContain("description", json);
    }

    [Fact]
    public void History_Entry_Written_Before_The_Field_Existed_Still_Loads()
    {
        const string legacy = """
            {"id":"abc","portal":"mine","title":"Senior Engineer","company":"Acme","location":"Copenhagen",
             "remoteMode":"remote","url":"https://acme.com/jobs/1","score":0.8,"reasoning":"Strong match.",
             "primaryStackHits":["Python"],"secondaryStackHits":[]}
            """;

        var match = JsonSerializer.Deserialize<ListingMatch>(legacy, JobmatchJsonOptions.Default)!;

        Assert.Equal("Senior Engineer", match.Title);
        Assert.Null(match.Description);
    }
}
