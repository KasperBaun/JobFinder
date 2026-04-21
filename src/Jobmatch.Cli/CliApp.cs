using System.Reflection;
using Jobmatch.Cli.Commands;
using Spectre.Console.Cli;

namespace Jobmatch.Cli;

public static class CliApp
{
    public static CommandApp Create()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("jobmatch");
            config.SetApplicationVersion(ReadAssemblyVersion());
            config.AddCommand<SkillsetCommand>("skillset")
                  .WithDescription("Author or refresh the active skillset profile.");
            config.AddCommand<ListingsCommand>("listings")
                  .WithDescription("Fetch, deduplicate, rank, and write the top-N shortlist.");
            config.AddCommand<VerifyCommand>("verify")
                  .WithDescription("Validate config files and portal connectivity.");
        });
        return app;
    }

    public static Task<int> RunAsync(string[] args) => Create().RunAsync(args);

    private static string ReadAssemblyVersion()
    {
        var asm = typeof(CliApp).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? (asm.GetName().Version?.ToString() ?? "0.0.0") : info;
    }
}
