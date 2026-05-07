using Jobmatch.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jobmatch.Tests.Endpoints;

public sealed class SystemEndpointsTests : IClassFixture<WebApplicationFactory<ApiProgram>>
{
    private readonly WebApplicationFactory<ApiProgram> _factory;

    public SystemEndpointsTests(WebApplicationFactory<ApiProgram> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ping_Returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(Routes.System.Ping);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Shutdown_Is_Not_Mapped_On_Standalone_Api()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync(Routes.System.Shutdown, content: null);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
