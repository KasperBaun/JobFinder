namespace Jobmatch.Api;

public sealed class ApiProgram
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var portEnv = Environment.GetEnvironmentVariable("JOBFINDER_PORT");
        if (!string.IsNullOrWhiteSpace(portEnv) &&
            int.TryParse(portEnv, out var port) &&
            port > 0 && port < 65536)
        {
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        }

        // Tests boot this via WebApplicationFactory with the "Testing" environment; skip the Hangfire
        // server there so no SQLite db is created and no worker thread starts during tests.
        var enableBackgroundJobs = !builder.Environment.IsEnvironment("Testing");
        builder.Services.AddJobmatchApi(enableBackgroundJobs);
        var app = builder.Build();
        app.MapJobmatchApi();
        await app.RunAsync();
    }
}
