using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobmatch.Api;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests.Endpoints;

public sealed class SkillsetExtractEndpointsTests : IDisposable
{
    private readonly string _tempRoot;

    public SkillsetExtractEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "cv-extract-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Mirror the API's JSON shape: camelCase properties and enums-as-strings.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private WebApplicationFactory<ApiProgram> Factory() =>
        new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(UserContext.Resolve(
                    emailOverride: "extract@test", repoRoot: _tempRoot, seedExamples: false))));

    [Fact]
    public async Task Status_FreshProcess_ReturnsIdle()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.Skillset.ExtractStatus);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CvExtractionStatusResponse>(Json);
        Assert.NotNull(dto);
        Assert.Equal(CvExtractionState.Idle, dto!.State);
        Assert.Null(dto.Profile);
    }

    [Fact]
    public async Task Start_WithNoInput_Returns400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        // A blank text field still counts as "no input" (a fully empty multipart body is
        // rejected by the framework's form reader before the handler runs).
        using var form = new MultipartFormDataContent
        {
            { new StringContent("  "), "text" },
        };
        var response = await client.PostAsync(Routes.Skillset.Extract, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("exactly one", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Start_WithTwoInputs_Returns400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var form = new MultipartFormDataContent
        {
            { new StringContent("my cv text"), "text" },
            { new StringContent("https://example.com/cv"), "url" },
        };
        var response = await client.PostAsync(Routes.Skillset.Extract, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The bundled default ranking.yml enables the LLM, but the temp data dir has no model
    // file — so a valid request must fail fast with the "download it first" gate, not hang
    // in a background run.
    [Fact]
    public async Task Start_WithoutModelOnDisk_Returns400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Jane Doe — Backend Developer, C#/.NET, Copenhagen"), "text" },
        };
        var response = await client.PostAsync(Routes.Skillset.Extract, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("model", await response.Content.ReadAsStringAsync());
    }
}
