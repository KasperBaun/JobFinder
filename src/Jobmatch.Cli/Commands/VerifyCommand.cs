using Jobmatch.Verification;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli.Commands;

public sealed class VerifyCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var verifier = new ConfigVerifier();
        var report = await verifier.VerifyAsync(includeConnectivity: false);

        var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "verification-report.md");
        await File.WriteAllTextAsync(reportPath, report.ToMarkdown());

        foreach (var check in report.Checks)
        {
            var (icon, colour) = check.Status switch
            {
                VerificationStatus.Pass => ("✓", "green"),
                VerificationStatus.Warn => ("!", "yellow"),
                _ => ("✗", "red"),
            };
            AnsiConsole.MarkupLine($"[{colour}]{icon}[/] [bold]{check.Name.EscapeMarkup()}[/] — {check.Details.EscapeMarkup()}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(report.HasFailures
            ? "[red]Verification failed.[/]"
            : report.HasWarnings
                ? "[yellow]Verification passed with warnings.[/]"
                : "[green]Verification passed.[/]");
        AnsiConsole.MarkupLine($"[dim]Report written to {reportPath}[/]");

        return report.HasFailures ? 1 : 0;
    }
}
