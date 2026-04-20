using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli.Commands;

public sealed class VerifyCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        AnsiConsole.MarkupLine("[yellow]verify[/] not yet implemented (structural checks arrive in Phase 2, connectivity in Phase 5).");
        return Task.FromResult(0);
    }
}
