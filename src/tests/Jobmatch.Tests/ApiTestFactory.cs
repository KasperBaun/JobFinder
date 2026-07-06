using Jobmatch.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jobmatch.Tests;

/// <summary>
/// Boots <see cref="ApiProgram"/> in the "Testing" environment, which disables the Hangfire background
/// server (no SQLite db, no worker thread) so endpoint tests stay hermetic and CI-safe.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<ApiProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
