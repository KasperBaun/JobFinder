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
            config.AddCommand<SkillsetCommand>("skillset")
                  .WithDescription("Author or refresh the active skillset profile.");
            config.AddCommand<ListingsCommand>("listings")
                  .WithDescription("Fetch, deduplicate, rank, and write the top-N shortlist.");
            config.AddCommand<VerifyCommand>("verify")
                  .WithDescription("Validate config files (and, from Phase 5, portal connectivity).");
        });
        return app;
    }

    public static Task<int> RunAsync(string[] args) => Create().RunAsync(args);
}
