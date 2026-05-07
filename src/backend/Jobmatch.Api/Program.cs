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

        builder.Services.AddJobmatchApi();
        var app = builder.Build();
        app.MapJobmatchApi();
        await app.RunAsync();
    }
}
