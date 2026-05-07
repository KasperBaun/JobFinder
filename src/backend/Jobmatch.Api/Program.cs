namespace Jobmatch.Api;

public sealed class ApiProgram
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddJobmatchApi();
        var app = builder.Build();
        app.MapJobmatchApi();
        await app.RunAsync();
    }
}
