using System.Reflection;
using Jobmatch.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli;

public static class CliApp
{
    public static CommandApp Create(IAnsiConsole? console = null)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            if (console is not null) config.ConfigureConsole(console);
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
