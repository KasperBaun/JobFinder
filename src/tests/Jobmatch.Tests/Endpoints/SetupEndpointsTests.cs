using System.Net;
using System.Net.Http.Json;
using Jobmatch.Api;
using Jobmatch.Api.Models;
using Jobmatch.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests.Endpoints;

public sealed class SetupEndpointsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _bootstrapPath;

    public SetupEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "setup-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _bootstrapPath = Path.Combine(_tempRoot, "bootstrap.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Override the BootstrapStore so the provider reads/writes a throwaway location.
    private WebApplicationFactory<ApiProgram> Factory() =>
        new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(new BootstrapStore(_bootstrapPath))));

    [Fact]
    public async Task Setup_Status_Then_Complete_RoundTrips()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<SetupStatusResponse>(Routes.Setup.Status);
        Assert.NotNull(before);
        Assert.False(before!.Configured);
        Assert.False(string.IsNullOrWhiteSpace(before.SuggestedDataDir));

        // A data endpoint should signal "setup required" (428) while unconfigured.
        var whoamiBefore = await client.GetAsync("/api/whoami");
        Assert.Equal((HttpStatusCode)428, whoamiBefore.StatusCode);

        var dataDir = Path.Combine(_tempRoot, "data");
        var complete = await client.PostAsJsonAsync(
            Routes.Setup.Complete, new SetupRequest("me@example.com", dataDir));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.True(completed!.Configured);

        var after = await client.GetFromJsonAsync<SetupStatusResponse>(Routes.Setup.Status);
        Assert.True(after!.Configured);
        Assert.True(File.Exists(_bootstrapPath));
        // No generic profile is seeded — the wizard guides the user to create their own.
        Assert.False(after.ProfileExists);
        Assert.False(File.Exists(Path.Combine(dataDir, "skillset.md")));

        // Now the data endpoint resolves.
        var whoamiAfter = await client.GetAsync("/api/whoami");
        Assert.Equal(HttpStatusCode.OK, whoamiAfter.StatusCode);

        // Creating a profile via PUT (create-or-update) flips profileExists true.
        var put = await client.PutAsJsonAsync("/api/skillset", new SkillsetUpdateRequest(
            Name: "Jane Doe", Location: "Copenhagen", ExperienceYears: 5,
            TargetRoles: ["Backend Engineer"], RemotePreference: "remote", Seniority: "senior",
            PrimaryStack: ["C#"], SecondaryStack: null, Domains: null, Disqualifiers: null,
            Languages: null, EmploymentTypes: null, Country: null, Region: null, Metro: null));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var afterProfile = await client.GetFromJsonAsync<SetupStatusResponse>(Routes.Setup.Status);
        Assert.True(afterProfile!.ProfileExists);
        Assert.True(File.Exists(Path.Combine(dataDir, "skillset.md")));
    }

    [Fact]
    public async Task Setup_Complete_WithMissingFields_Returns_400()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Routes.Setup.Complete, new SetupRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
