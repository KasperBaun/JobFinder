using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Jobmatch;
using Jobmatch.Api;
using Jobmatch.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests.Endpoints;

public sealed class ConfigTransferEndpointsTests : IDisposable
{
    private readonly string _tempRoot;

    public ConfigTransferEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "cfg-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Override the process-singleton UserContext so the endpoints operate on a throwaway temp
    // directory instead of the developer's real data/<email>/ folder.
    private WebApplicationFactory<ApiProgram> Factory(string email = "endpoint@test") =>
        new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(UserContext.Resolve(
                    emailOverride: email, repoRoot: _tempRoot, seedExamples: false))));

    [Fact]
    public async Task Export_Returns_Zip_With_Attachment()
    {
        var ctx = UserContext.Resolve(emailOverride: "endpoint@test", repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(ctx.SkillsetPath, "# skills");

        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.Config.Export);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Import_RoundTrips_Exported_Archive()
    {
        var ctx = UserContext.Resolve(emailOverride: "endpoint@test", repoRoot: _tempRoot, seedExamples: false);
        File.WriteAllText(ctx.SkillsetPath, "# skills");

        using var factory = Factory();
        using var client = factory.CreateClient();

        var zipBytes = await client.GetByteArrayAsync(Routes.Config.Export);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "jobfinder-export.zip");

        var response = await client.PostAsync(Routes.Config.Import, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(dto);
        Assert.True(dto!.Restored >= 1);
    }

    [Fact]
    public async Task Import_WithoutFile_Returns_400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var response = await client.PostAsync(Routes.Config.Import, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
