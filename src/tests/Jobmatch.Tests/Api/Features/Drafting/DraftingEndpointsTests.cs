using System.Net;
using System.Net.Http.Json;
using Jobmatch.Api;
using Jobmatch.Api.Features.Drafting;
using Jobmatch.Api.Features.Setup;
using Jobmatch.Features.Bootstrap;
using Jobmatch.Infrastructure.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests.Api.Features.Drafting;

public sealed class DraftingEndpointsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _bootstrapPath;

    public DraftingEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "drafting-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _bootstrapPath = Path.Combine(_tempRoot, "bootstrap.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private ApiTestFactory Factory() => new()
    {
        ConfigureTestServices = services => services.AddSingleton(new BootstrapStore(_bootstrapPath)),
    };

    private async Task<HttpClient> ReadyClient(ApiTestFactory factory)
    {
        var client = factory.CreateClient();
        var complete = await client.PostAsJsonAsync(
            Routes.Setup.Complete, new SetupRequest("draft@example.com", Path.Combine(_tempRoot, "data")));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return client;
    }

    [Fact]
    public async Task GetCv_BeforeAnythingIsStored_ReturnsNullText()
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var body = await client.GetFromJsonAsync<CvResponse>(Routes.Cv.Get);

        Assert.NotNull(body);
        Assert.Null(body!.Text);
    }

    [Fact]
    public async Task PutCv_ThenGetCv_RoundTrips()
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var put = await client.PutAsJsonAsync(Routes.Cv.Update, new CvUpdateRequest("Jane Doe\nDeveloper at Acme"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await client.GetFromJsonAsync<CvResponse>(Routes.Cv.Get);
        Assert.Contains("Developer at Acme", body!.Text);
    }

    [Fact]
    public async Task PutCv_BlankText_IsRejected()
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var put = await client.PutAsJsonAsync(Routes.Cv.Update, new CvUpdateRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Status_BeforeAnyDraft_IsIdle()
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var body = await client.GetFromJsonAsync<DraftStatusResponse>(Routes.Drafting.Status, JobmatchJsonOptions.Default);

        Assert.NotNull(body);
        Assert.Equal(DraftState.Idle, body!.State);
        Assert.Null(body.Result);
    }

    [Theory]
    [InlineData("", "listing-1")]
    [InlineData("run-1", "")]
    [InlineData("   ", "   ")]
    public async Task Draft_MissingIdentifiers_IsRejected(string runId, string listingId)
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var post = await client.PostAsJsonAsync(Routes.Drafting.Draft, new DraftRequest(runId, listingId));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    // The run does not exist, so the background attempt fails — but accepting the request must not
    // itself fail, because the caller polls for the outcome rather than reading it off this response.
    [Fact]
    public async Task Draft_UnknownRun_IsAcceptedThenReportedFailedByStatus()
    {
        using var factory = Factory();
        using var client = await ReadyClient(factory);

        var post = await client.PostAsJsonAsync(
            Routes.Drafting.Draft, new DraftRequest("20260701-100000-aaaaaa", "listing-1"));
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);

        var final = await PollUntilSettled(client);
        Assert.Equal(DraftState.Failed, final.State);
        Assert.False(string.IsNullOrWhiteSpace(final.Error));
    }

    private static async Task<DraftStatusResponse> PollUntilSettled(HttpClient client)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var body = await client.GetFromJsonAsync<DraftStatusResponse>(Routes.Drafting.Status, JobmatchJsonOptions.Default);
            if (body!.State is DraftState.Completed or DraftState.Failed) return body;
            await Task.Delay(50);
        }

        throw new Xunit.Sdk.XunitException("Draft never left the Drafting state.");
    }
}
