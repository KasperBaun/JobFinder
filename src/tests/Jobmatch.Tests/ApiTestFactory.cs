using Jobmatch.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Tests;

/// <summary>
/// Boots <see cref="ApiProgram"/> in the "Testing" environment, which disables the Hangfire background
/// server (no SQLite db, no worker thread) so endpoint tests stay hermetic and CI-safe. Set
/// <see cref="ConfigureTestServices"/> to stage per-test dependencies, e.g. a <c>BootstrapStore</c>
/// pointed at a temp directory.
/// </summary>
/// <remarks>
/// Every endpoint test must go through this factory. Constructing
/// <c>WebApplicationFactory&lt;ApiProgram&gt;</c> directly skips the environment override below and
/// starts a real job server against the developer's data directory.
/// It keeps a single public parameterless constructor because xUnit's <c>IClassFixture</c> accepts
/// nothing else — per-test wiring goes through the init-only property instead.
/// </remarks>
public sealed class ApiTestFactory : WebApplicationFactory<ApiProgram>
{
    public Action<IServiceCollection>? ConfigureTestServices { get; init; }

    // ConfigureWebHost is not enough on its own. WebApplicationFactory applies it while the host is
    // being built, but ApiProgram.Main reads builder.Environment to decide whether to start Hangfire
    // before that point, so the override arrives too late. Setting the variable
    // WebApplication.CreateBuilder itself reads is what actually reaches the decision.
    static ApiTestFactory() => Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (ConfigureTestServices is not null)
            builder.ConfigureServices(ConfigureTestServices);
    }
}
