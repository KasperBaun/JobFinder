using Jobmatch.Domain.Runs;
using Jobmatch.Features.Providers;
using Jobmatch.Search;
using Jobmatch.Infrastructure.IO;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Features.Providers;

/// <summary>
/// A source added through the add-a-source flow (R-090) must be fetched by an actual search run, not
/// merely listed on the providers page. The two used to disagree: the providers service composed the
/// shipped catalog with user-providers.json, while the search orchestrator read only the shipped
/// catalog, so user-added sources were silently never searched.
/// </summary>
public sealed class UserAddedSourceReachesTheRunTests : IDisposable
{
    private static readonly IFileSystem Fs = new PhysicalFileSystem();
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    private readonly JobmatchUserContext _ctx;

    public UserAddedSourceReachesTheRunTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "user-source-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
        _ctx = JobmatchUserContext.Resolve(
            emailOverride: "usersource@example.com", repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(_ctx.SkillsetPath, MinimalSkillset);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
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

        - C#
        """;

    /// <summary>A manual source reads a CSV from imports/ rather than the network, so a full run
    /// stays hermetic.</summary>
    private int AddManualUserSource(string name)
    {
        var draft = new PortalConfig(Name: name, Type: PortalType.Manual, Enabled: true);
        return UserProviderStore.Add(_ctx.UserProvidersPath, draft, catalog: []).Id;
    }

    /// <summary>Opts out of every shipped source, leaving the user's own as the only one enabled.</summary>
    private void DisableShippedSources()
    {
        var shippedIds = new ProviderCatalog(_ctx).Shipped().Select(p => p.Id);
        File.WriteAllText(_ctx.ProviderStatePath, $"{{\"disabled\":[{string.Join(",", shippedIds)}]}}");
    }

    /// <summary>ManualAdapter globs "{provider name}-*.*" inside imports/, hence the suffix.</summary>
    private void StageImport(string name, string csv) =>
        File.WriteAllText(Path.Combine(_ctx.ImportsDir, $"{name}-export.csv"), csv);

    private SearchPipeline NewService() => new(
        _ctx, new ProviderCatalog(_ctx), TestServices.Runs(_ctx), Fs);

    private static async Task<List<SearchProgressEvent>> Drain(IAsyncEnumerable<SearchProgressEvent> events)
    {
        var result = new List<SearchProgressEvent>();
        await foreach (var e in events) result.Add(e);
        return result;
    }

    [Fact]
    public async Task ARunFetchesTheSourceTheUserAdded()
    {
        AddManualUserSource("my-own-board");
        DisableShippedSources();
        StageImport("my-own-board", """
            title,company,location,url,description
            Senior .NET Developer,Own Co,Copenhagen,https://own.example/jobs/1,Working with C# every day
            """);

        var events = await Drain(NewService().RunAsync(new SearchRequest()));

        var started = Assert.IsType<StartedEvent>(events[0]);
        Assert.Equal(1, started.Total);

        var done = events.OfType<ProviderDoneEvent>().Single();
        Assert.Equal("my-own-board", done.Provider);
        Assert.Equal(1, done.FetchedCount);
    }

    [Fact]
    public async Task ARunSkipsAUserSourceTheUserTurnedOff()
    {
        var id = AddManualUserSource("my-own-board");
        var shippedIds = new ProviderCatalog(_ctx).Shipped().Select(p => p.Id).Append(id);
        File.WriteAllText(_ctx.ProviderStatePath, $"{{\"disabled\":[{string.Join(",", shippedIds)}]}}");

        var events = await Drain(NewService().RunAsync(new SearchRequest()));

        Assert.Equal(0, Assert.IsType<StartedEvent>(events[0]).Total);
    }
}
