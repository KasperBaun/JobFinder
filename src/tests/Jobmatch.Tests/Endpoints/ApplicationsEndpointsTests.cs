using System.Net;
using System.Net.Http.Json;
using Jobmatch.Api;
using Jobmatch.Api.Models;
using Jobmatch.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests.Endpoints;

public sealed class ApplicationsEndpointsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _bootstrapPath;

    public ApplicationsEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "applications-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _bootstrapPath = Path.Combine(_tempRoot, "bootstrap.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private WebApplicationFactory<ApiProgram> Factory() =>
        new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new BootstrapStore(_bootstrapPath))));

    private static async Task CompleteSetup(HttpClient client, string dataDir)
    {
        var complete = await client.PostAsJsonAsync(
            Routes.Setup.Complete, new SetupRequest("apps@example.com", dataDir));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    [Fact]
    public async Task SetStatus_ThenListApplications_RoundTrips()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await CompleteSetup(client, Path.Combine(_tempRoot, "data"));

        var set = await client.PostAsJsonAsync(
            Routes.Marks.SetStatus, new MarkStatusRequest("20260701-100000-aaaaaa", "l1", "applied"));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var setBody = await set.Content.ReadFromJsonAsync<MarkResponse>();
        Assert.True(setBody!.Success);

        // No history file exists for the run, so the entry is unresolvable — but the endpoint
        // must still answer cleanly with an empty aggregation.
        var list = await client.GetFromJsonAsync<ApplicationsResponse>(Routes.Applications.GetAll);
        Assert.NotNull(list);
        Assert.Empty(list!.Applications);
    }

    [Fact]
    public async Task SetStatus_WithHistory_ResponseCarriesStatusChangedAt()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var dataDir = Path.Combine(_tempRoot, "data");
        await CompleteSetup(client, dataDir);

        const string runId = "20260701-100000-aaaaaa";
        WriteHistory(dataDir, runId);

        var before = DateTimeOffset.UtcNow;
        var set = await client.PostAsJsonAsync(
            Routes.Marks.SetStatus, new MarkStatusRequest(runId, "l1", "applied"));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var list = await client.GetFromJsonAsync<ApplicationsResponse>(Routes.Applications.GetAll);
        var entry = Assert.Single(list!.Applications);
        Assert.Equal("applied", entry.Status);
        Assert.NotNull(entry.StatusChangedAt);
        Assert.InRange(entry.StatusChangedAt!.Value, before, DateTimeOffset.UtcNow);
    }

    private static void WriteHistory(string dataDir, string runId)
    {
        var detail = new Jobmatch.Search.RunDetail(
            RunId: runId,
            StartedAt: DateTimeOffset.UtcNow,
            Providers: [],
            FetchedCount: 1,
            DedupedCount: 1,
            RankedCount: 1,
            ShortlistCount: 1,
            TopScore: 0.8,
            GoodMarks: 0,
            Shortlist: [new Jobmatch.Search.ListingMatch(
                "l1", "portal", "Role A", "Co A", "Copenhagen", "onsite", "https://x/l1", null, 0.8, "", [], [])],
            Marks: new Dictionary<string, string>());
        var historyDir = Path.Combine(dataDir, "history");
        Directory.CreateDirectory(historyDir);
        File.WriteAllText(
            Path.Combine(historyDir, $"{runId}.json"),
            System.Text.Json.JsonSerializer.Serialize(detail, Jobmatch.Json.JobmatchJsonOptions.Indented));
    }

    [Fact]
    public async Task SetStatus_InvalidValue_Returns400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await CompleteSetup(client, Path.Combine(_tempRoot, "data"));

        var set = await client.PostAsJsonAsync(
            Routes.Marks.SetStatus, new MarkStatusRequest("20260701-100000-aaaaaa", "l1", "ghosted"));
        Assert.Equal(HttpStatusCode.BadRequest, set.StatusCode);
    }
}
