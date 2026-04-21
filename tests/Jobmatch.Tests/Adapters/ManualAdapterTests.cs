using Jobmatch.Adapters;
using Jobmatch.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Adapters;

public sealed class ManualAdapterTests : IDisposable
{
    private readonly string _imports;

    public ManualAdapterTests()
    {
        _imports = Path.Combine(Path.GetTempPath(), "jobmatch-tests", Guid.NewGuid().ToString(), "imports");
        Directory.CreateDirectory(_imports);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_imports)!, recursive: true); } catch { /* best effort */ }
    }

    private PortalConfig PortalNamed(string name) => new(Name: name, Type: PortalType.Manual, Enabled: true);

    [Fact]
    public async Task FetchAsync_JsonFile_Returns_Listings()
    {
        File.WriteAllText(Path.Combine(_imports, "mine-2026-04-20.json"),
            """
            [
              {
                "title": "Platform Engineer",
                "company": "Acme",
                "location": "Copenhagen",
                "url": "https://acme.com/jobs/1",
                "description": "We need cloud skills.",
                "posted_at": "2026-04-01T09:00:00Z"
              }
            ]
            """);

        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("mine"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Single(results);
        Assert.Equal("Platform Engineer", results[0].Title);
        Assert.Equal("Acme", results[0].Company);
        Assert.Equal("https://acme.com/jobs/1", results[0].Url.ToString());
        Assert.NotNull(results[0].PostedAt);
    }

    [Fact]
    public async Task FetchAsync_CsvFile_With_Quoted_Commas_Parses_Correctly()
    {
        File.WriteAllText(Path.Combine(_imports, "mine-exports.csv"),
            "title,company,location,url,description,posted_at\n" +
            "\"Senior Engineer, Platform\",Acme,Copenhagen,https://acme.com/1,\"Rust, Go, K8s\",2026-03-15\n" +
            "Backend Dev,Initech,Berlin,https://initech.com/2,Java,2026-03-20\n");

        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("mine"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Equal(2, results.Count);
        Assert.Equal("Senior Engineer, Platform", results[0].Title);
        Assert.Equal("Rust, Go, K8s", results[0].Description);
        Assert.Equal("Backend Dev", results[1].Title);
    }

    [Fact]
    public async Task FetchAsync_Missing_Url_Or_Title_Rows_Are_Skipped()
    {
        File.WriteAllText(Path.Combine(_imports, "mine-partial.json"),
            """
            [
              { "title": "Good", "url": "https://a.com/1" },
              { "title": "No URL" },
              { "url": "https://a.com/2" }
            ]
            """);

        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("mine"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Single(results);
    }

    [Fact]
    public async Task FetchAsync_No_Matching_Files_Returns_Empty()
    {
        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("absent"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task FetchAsync_Infers_Hybrid_Before_Remote()
    {
        // "Hybrid role with remote Fridays" must be classified as Hybrid, not Remote.
        File.WriteAllText(Path.Combine(_imports, "mine-1.json"),
            """
            [
              {
                "title": "Engineer",
                "company": "Acme",
                "location": "Copenhagen",
                "url": "https://acme.com/jobs/1",
                "description": "Hybrid role with remote Fridays."
              }
            ]
            """);

        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("mine"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Equal(RemoteMode.Hybrid, results[0].RemoteMode);
    }

    [Fact]
    public async Task FetchAsync_Missing_Imports_Directory_Returns_Empty()
    {
        Directory.Delete(_imports);
        using var http = new HttpClient();
        var adapter = new ManualAdapter(PortalNamed("mine"), http, NullLogger.Instance, _imports);

        var results = await adapter.FetchAsync();
        Assert.Empty(results);
    }
}
