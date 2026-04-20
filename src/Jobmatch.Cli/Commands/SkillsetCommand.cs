using System.ComponentModel;
using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jobmatch.Cli.Commands;

public sealed class SkillsetCommand : AsyncCommand<SkillsetCommand.Settings>
{
    private const string SkillsetPath = "config/skillset.md";

    public sealed class Settings : CommandSettings
    {
        [Description("CV file path or URL to display alongside the prompts for reference.")]
        [CommandOption("--from <SOURCE>")]
        public string? From { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.From))
        {
            var reference = await LoadReferenceAsync(settings.From);
            if (reference is not null)
            {
                AnsiConsole.MarkupLine($"[dim]Loaded {reference.Length} characters from[/] [cyan]{settings.From.EscapeMarkup()}[/][dim]. Use as reference while answering.[/]");
                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.MarkupLine("[bold]Author a jobmatch skillset.[/] Press Ctrl+C to cancel.");
        AnsiConsole.WriteLine();

        Skillset skillset;
        try
        {
            skillset = Collect();
        }
        catch (ConfigException ex)
        {
            AnsiConsole.MarkupLine($"[red]Invalid input:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Preview of skillset:[/]");
        AnsiConsole.Write(new Panel(SkillsetParser.Serialize(skillset).EscapeMarkup()).Header("config/skillset.md"));

        if (File.Exists(SkillsetPath))
        {
            AnsiConsole.MarkupLine($"[yellow]{SkillsetPath} already exists.[/] Diff (existing → new):");
            PrintDiff(File.ReadAllText(SkillsetPath), SkillsetParser.Serialize(skillset));

            if (!AnsiConsole.Confirm($"Overwrite [yellow]{SkillsetPath}[/]?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[red]Cancelled — no changes written.[/]");
                return 1;
            }
        }

        SkillsetParser.Save(skillset, SkillsetPath);
        AnsiConsole.MarkupLine($"[green]Wrote[/] {SkillsetPath}.");
        AnsiConsole.MarkupLine($"[dim]Next: run [yellow]verify[/] to check your config.[/]");
        return 0;
    }

    private static Skillset Collect()
    {
        var name = AnsiConsole.Ask<string>("[yellow]Name[/]:");
        var location = AnsiConsole.Ask<string>("[yellow]Location[/] (city, country or 'Remote'):");
        var experience = AnsiConsole.Ask<int>("[yellow]Years of experience[/]:");
        var targetRoles = AskList("target roles");
        var remotePref = AnsiConsole.Prompt(new SelectionPrompt<RemotePreference>()
            .Title("[yellow]Remote preference[/]:")
            .AddChoices(Enum.GetValues<RemotePreference>()));
        var seniority = AnsiConsole.Prompt(new SelectionPrompt<Seniority>()
            .Title("[yellow]Seniority[/]:")
            .AddChoices(Enum.GetValues<Seniority>()));
        var primary = AskList("primary stack (must-have keywords)");
        var secondary = AskList("secondary stack (nice-to-have keywords)");
        var domains = AskList("domains (e.g. fintech, developer tools, healthcare)");
        var disqualifiers = AskList("disqualifiers (deal-breaker keywords)");
        var languages = AskList("spoken languages");
        var employmentTypes = AskList("acceptable employment types");

        return new Skillset(
            Name: name.Trim(),
            Location: location.Trim(),
            ExperienceYears: experience,
            TargetRoles: targetRoles,
            RemotePreference: remotePref,
            Seniority: seniority,
            PrimaryStack: primary,
            SecondaryStack: secondary,
            Domains: domains,
            Disqualifiers: disqualifiers,
            Languages: languages,
            EmploymentTypes: employmentTypes);
    }

    private static IReadOnlyList<string> AskList(string label)
    {
        var raw = AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]{label}[/] [dim](comma-separated, blank to skip)[/]:")
                .AllowEmpty());
        return raw.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static async Task<string?> LoadReferenceAsync(string source)
    {
        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                return await http.GetStringAsync(uri);
            }
            if (File.Exists(source))
            {
                return await File.ReadAllTextAsync(source);
            }
            AnsiConsole.MarkupLine($"[yellow]--from source not reachable: {source.EscapeMarkup()}[/]");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not load --from source: {ex.Message.EscapeMarkup()}[/]");
            return null;
        }
    }

    private static void PrintDiff(string existing, string proposed)
    {
        var existingLines = existing.Replace("\r\n", "\n").Split('\n');
        var proposedLines = proposed.Replace("\r\n", "\n").Split('\n');
        var existingSet = new HashSet<string>(existingLines);
        var proposedSet = new HashSet<string>(proposedLines);

        foreach (var line in existingLines)
        {
            if (!proposedSet.Contains(line))
            {
                AnsiConsole.MarkupLine($"[red]- {line.EscapeMarkup()}[/]");
            }
        }
        foreach (var line in proposedLines)
        {
            if (!existingSet.Contains(line))
            {
                AnsiConsole.MarkupLine($"[green]+ {line.EscapeMarkup()}[/]");
            }
        }
    }
}
