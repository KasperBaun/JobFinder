using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli.Commands;

public sealed class SkillsetCommand : AsyncCommand<SkillsetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("CV file path or URL to seed the skillset from.")]
        [CommandOption("--from <SOURCE>")]
        public string? From { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[yellow]skillset[/] not yet implemented (arrives in Phase 2).");
        return Task.FromResult(0);
    }
}
