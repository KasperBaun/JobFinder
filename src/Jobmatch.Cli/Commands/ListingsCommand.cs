using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli.Commands;

public sealed class ListingsCommand : AsyncCommand<ListingsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Only run the named portal.")]
        [CommandOption("--portal <NAME>")]
        public string? Portal { get; init; }

        [Description("Override the top-N cutoff from ranking.yml.")]
        [CommandOption("--top <N>")]
        public int? Top { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[yellow]listings[/] not yet implemented (arrives in Phase 3/4).");
        return Task.FromResult(0);
    }
}
