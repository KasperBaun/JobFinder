using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class PortalConfigLoaderTests
{
    [Fact]
    public void Parse_Valid_Yaml_Returns_Portals()
    {
        var yaml = """
            portals:
              - name: jobnet
                type: api
                enabled: true
                endpoint: https://job.jobnet.dk/CV/FindWork/Search
                query_params:
                  SearchString: "software engineer"
                rate_limit_rps: 2.0
              - name: some-rss
                type: rss
                enabled: false
                endpoint: https://example.com/feed.rss
              - name: jobindex
                type: manual
                enabled: false
            """;

        var portals = PortalConfigLoader.Parse(yaml);

        Assert.Equal(3, portals.Count);
        Assert.Equal("jobnet", portals[0].Name);
        Assert.Equal(PortalType.Api, portals[0].Type);
        Assert.True(portals[0].Enabled);
        Assert.Equal(2.0, portals[0].RateLimitRps);
        Assert.NotNull(portals[0].QueryParams);
        Assert.Equal("software engineer", portals[0].QueryParams!["SearchString"]?.ToString());

        Assert.Equal(PortalType.Rss, portals[1].Type);
        Assert.False(portals[1].Enabled);

        Assert.Equal(PortalType.Manual, portals[2].Type);
    }

    [Fact]
    public void Parse_Missing_PortalsKey_Throws()
    {
        var yaml = """
            somethingelse:
              - name: x
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalConfigLoader.Parse(yaml));
        Assert.Contains("portals", ex.Message);
    }

    [Fact]
    public void Parse_Invalid_PortalType_Throws()
    {
        var yaml = """
            portals:
              - name: bad
                type: telepathy
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalConfigLoader.Parse(yaml));
        Assert.Contains("type", ex.Message);
    }

    [Fact]
    public void Parse_Missing_Name_Throws()
    {
        var yaml = """
            portals:
              - type: api
                endpoint: https://example.com
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalConfigLoader.Parse(yaml));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void Parse_Defaults_Enabled_To_True()
    {
        var yaml = """
            portals:
              - name: x
                type: manual
            """;
        var portals = PortalConfigLoader.Parse(yaml);
        Assert.True(portals[0].Enabled);
    }

    [Fact]
    public void Parse_StaticFields_Are_Loaded()
    {
        var yaml = """
            portals:
              - name: pleo
                type: api
                endpoint: https://boards-api.greenhouse.io/v1/boards/pleo/jobs
                static_fields:
                  company: "Pleo"
                  location: "Copenhagen"
            """;

        var portals = PortalConfigLoader.Parse(yaml);

        Assert.NotNull(portals[0].StaticFields);
        Assert.Equal("Pleo", portals[0].StaticFields!["company"]);
        Assert.Equal("Copenhagen", portals[0].StaticFields!["location"]);
    }

    [Fact]
    public void Parse_Without_StaticFields_Block_Yields_Null()
    {
        var yaml = """
            portals:
              - name: x
                type: manual
            """;
        var portals = PortalConfigLoader.Parse(yaml);
        Assert.Null(portals[0].StaticFields);
    }

    [Fact]
    public void Load_Shipped_Example_File_Parses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "portals.example.yml");
        Assert.True(File.Exists(path), $"expected example file at {path}");

        var portals = PortalConfigLoader.Load(path);
        Assert.NotEmpty(portals);

        var greenhouse = portals.Where(p => p.Name.StartsWith("greenhouse-", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(greenhouse);
        Assert.All(greenhouse, p =>
        {
            Assert.Equal(PortalType.Api, p.Type);
            Assert.NotNull(p.StaticFields);
            Assert.True(p.StaticFields!.ContainsKey("company"));
        });
    }

    [Fact]
    public void Parse_Method_And_BodyTemplate_Are_Loaded()
    {
        var yaml = """
            portals:
              - name: jooble
                type: api
                method: post
                endpoint: https://example.com/api/{api_key}
                query_params:
                  api_key: "ABC"
                body_template:
                  keywords: "software"
                  page: 1
            """;
        var portals = PortalConfigLoader.Parse(yaml);

        Assert.Equal("post", portals[0].Method, ignoreCase: true);
        Assert.NotNull(portals[0].BodyTemplate);
        Assert.Equal("software", portals[0].BodyTemplate!["keywords"]?.ToString());
        Assert.Equal("1", portals[0].BodyTemplate!["page"]?.ToString());
    }

    [Fact]
    public void Parse_Pagination_Block_Loaded()
    {
        var yaml = """
            portals:
              - name: paged
                type: api
                endpoint: https://example.com/jobs
                pagination:
                  param: page
                  start: 1
                  step: 1
                  size_param: page_size
                  size: 50
                  max_pages: 3
            """;
        var portals = PortalConfigLoader.Parse(yaml);
        var p = portals[0].Pagination;
        Assert.NotNull(p);
        Assert.Equal("page", p!.Param);
        Assert.Equal(1, p.Start);
        Assert.Equal(1, p.Step);
        Assert.Equal("page_size", p.SizeParam);
        Assert.Equal(50, p.Size);
        Assert.Equal(3, p.MaxPages);
    }

    [Fact]
    public void Parse_Without_Pagination_Yields_Null()
    {
        var yaml = """
            portals:
              - name: x
                type: api
                endpoint: https://example.com
            """;
        var portals = PortalConfigLoader.Parse(yaml);
        Assert.Null(portals[0].Pagination);
    }

    [Fact]
    public void Parse_Without_Method_Yields_Null()
    {
        var yaml = """
            portals:
              - name: x
                type: api
                endpoint: https://example.com
            """;
        var portals = PortalConfigLoader.Parse(yaml);
        Assert.Null(portals[0].Method);
        Assert.Null(portals[0].BodyTemplate);
    }

    [Fact]
    public void Parse_Invalid_Url_Throws()
    {
        var yaml = """
            portals:
              - name: x
                type: api
                endpoint: "::not-a-url::"
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalConfigLoader.Parse(yaml));
        Assert.Contains("endpoint", ex.Message);
    }
}
