using System.Text.Json;
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class PortalCatalogLoaderTests
{
    [Fact]
    public void Parse_Nested_BodyTemplate_Object_Survives_Document_Disposal()
    {
        // Nested objects in bodyTemplate (e.g. Workday's "appliedFacets": {}) must be
        // cloned out of the backing JsonDocument, which is disposed when Parse returns.
        var json = """
            { "version": 1, "providers": [
              {
                "id": 1, "name": "workday-x", "type": "api", "enabled": true,
                "endpoint": "https://x.wd3.myworkdayjobs.com/wday/cxs/x/X/jobs",
                "method": "post",
                "bodyTemplate": { "searchText": "software", "appliedFacets": { "locationCountry": ["dk"] } },
                "responseMapping": { "items_path": "jobPostings", "id": "externalPath", "title": "title", "url_template": "https://x/{externalPath}" }
              }
            ] }
            """;

        var portals = PortalCatalogLoader.Parse(json);

        var serialized = JsonSerializer.Serialize(portals[0].BodyTemplate);
        Assert.Contains("locationCountry", serialized);
    }

    [Fact]
    public void Parse_MinimalProvider()
    {
        var json = """
            {
              "version": 1,
              "providers": [
                {
                  "id": 1,
                  "name": "greenhouse-pleo",
                  "type": "api",
                  "enabled": true,
                  "endpoint": "https://boards-api.greenhouse.io/v1/boards/pleo/jobs",
                  "queryParams": { "content": "true" },
                  "responseMapping": {
                    "items_path": "jobs",
                    "id": "id",
                    "title": "title",
                    "url": "absolute_url"
                  },
                  "staticFields": { "company": "Pleo" },
                  "rateLimitRps": 1.0
                }
              ]
            }
            """;

        var portals = PortalCatalogLoader.Parse(json);

        Assert.Single(portals);
        var p = portals[0];
        Assert.Equal(1, p.Id);
        Assert.Equal("greenhouse-pleo", p.Name);
        Assert.Equal(PortalType.Api, p.Type);
        Assert.True(p.Enabled);
        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/pleo/jobs", p.Endpoint?.ToString());
        Assert.Equal("true", p.QueryParams!["content"]?.ToString());
        Assert.Equal("Pleo", p.StaticFields!["company"]);
        Assert.Null(p.RequiresSecret);
    }

    [Fact]
    public void Parse_ProviderRequiringSecret()
    {
        var json = """
            {
              "version": 1,
              "providers": [
                {
                  "id": 5,
                  "name": "jooble",
                  "type": "api",
                  "enabled": true,
                  "endpoint": "https://jooble.org/api/{api_key}",
                  "method": "post",
                  "bodyTemplate": { "keywords": "developer" },
                  "queryParams": { "api_key": "" },
                  "responseMapping": { "items_path": "jobs", "id": "id", "title": "title", "url": "link" },
                  "rateLimitRps": 1.0,
                  "requiresSecret": "api_key"
                }
              ]
            }
            """;

        var portals = PortalCatalogLoader.Parse(json);
        Assert.Equal("api_key", portals[0].RequiresSecret);
        Assert.Equal("post", portals[0].Method);
    }

    [Fact]
    public void Parse_MissingProvidersKey_Throws()
    {
        var json = """{ "version": 1 }""";
        var ex = Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
        Assert.Contains("providers", ex.Message);
    }

    [Fact]
    public void Parse_DuplicateIds_Throws()
    {
        var json = """
            { "version": 1, "providers": [
              { "id": 1, "name": "a", "type": "manual", "enabled": true },
              { "id": 1, "name": "b", "type": "manual", "enabled": true }
            ] }
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_InvalidType_Throws()
    {
        var json = """
            { "version": 1, "providers": [
              { "id": 1, "name": "x", "type": "carrier-pigeon", "enabled": true }
            ] }
            """;
        Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
    }

    [Fact]
    public void Bundled_PortalsJson_Parses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "portals.json");
        Assert.True(File.Exists(path), $"missing: {path}");
        var portals = PortalCatalogLoader.Load(path);
        Assert.NotEmpty(portals);
        Assert.All(portals, p => Assert.True(p.Id > 0, $"provider '{p.Name}' missing id"));
        Assert.Equal(portals.Count, portals.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Bundled_PortalsJson_Has_GreenhouseStaticCompany()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "portals.json");
        var portals = PortalCatalogLoader.Load(path);
        var greenhouseWithCompany = portals.FirstOrDefault(p =>
            p.Name.StartsWith("greenhouse-", StringComparison.Ordinal)
            && p.Type == PortalType.Api
            && p.StaticFields is { Count: > 0 }
            && p.StaticFields.TryGetValue("company", out var c)
            && !string.IsNullOrWhiteSpace(c));
        Assert.NotNull(greenhouseWithCompany);
    }
}
