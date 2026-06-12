using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

// Invariants over the bundled portals.json — catches catalog entries that would
// misbehave at runtime (hammering the Jobindex backend, manual providers without
// import instructions, POST APIs without a body).
public sealed class PortalCatalogInvariantTests
{
    private static IReadOnlyList<PortalConfig> LoadBundled() =>
        PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));

    [Fact]
    public void Jobindex_Backend_Rss_Feeds_Are_Enriched_And_Throttled()
    {
        var feeds = LoadBundled().Where(p =>
            p.Type == PortalType.Rss
            && p.Endpoint is not null
            && (p.Endpoint.Host.Contains("jobindex.dk", StringComparison.OrdinalIgnoreCase)
                || p.Endpoint.Host.Contains("it-jobbank.dk", StringComparison.OrdinalIgnoreCase))
            && p.Enabled).ToList();

        Assert.NotEmpty(feeds);
        Assert.All(feeds, p =>
        {
            Assert.True(p.EnrichBody, $"'{p.Name}': jobindex-backend feeds need enrichBody for full descriptions");
            Assert.True(p.RateLimitRps <= 0.5, $"'{p.Name}': jobindex-backend feeds must stay at <= 0.5 rps");
        });
    }

    [Fact]
    public void Manual_Providers_Document_The_Imports_Flow()
    {
        var manuals = LoadBundled().Where(p => p.Type == PortalType.Manual).ToList();

        Assert.NotEmpty(manuals);
        Assert.All(manuals, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Notes), $"'{p.Name}': manual providers need user-facing notes");
            Assert.Contains("imports", p.Notes, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Post_Api_Providers_Have_Body_And_Items_Path()
    {
        var posts = LoadBundled().Where(p =>
            p.Type == PortalType.Api
            && string.Equals(p.Method, "post", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.NotEmpty(posts);
        Assert.All(posts, p =>
        {
            Assert.NotNull(p.BodyTemplate);
            Assert.NotNull(p.ResponseMapping);
            Assert.True(p.ResponseMapping!.ContainsKey("items_path"), $"'{p.Name}': missing items_path");
        });
    }
}
