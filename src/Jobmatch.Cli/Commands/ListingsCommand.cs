using System.ComponentModel;
using Jobmatch;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.Models;
using Jobmatch.Output;
using Microsoft.Extensions.Logging.Abstractions;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var root = Directory.GetCurrentDirectory();

        var portalsPath = Path.Combine(root, "config", "portals.yml");
        if (!File.Exists(portalsPath))
        {
            AnsiConsole.MarkupLine($"[red]Missing {portalsPath}. Copy config/portals.example.yml to get started.[/]");
            return 1;
        }

        IReadOnlyList<PortalConfig> portals;
        try
        {
            portals = PortalConfigLoader.Load(portalsPath);
        }
        catch (ConfigException ex)
        {
            AnsiConsole.MarkupLine($"[red]Cannot read portals.yml:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }

        var enabled = portals
            .Where(p => p.Enabled)
            .Where(p => settings.Portal is null || p.Name.Equals(settings.Portal, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (enabled.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No enabled portals match the filter.[/]");
            return 1;
        }

        var imports = Path.Combine(root, "data", "imports");
        var raw = Path.Combine(root, "data", "raw");
        Directory.CreateDirectory(raw);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var logger = NullLogger.Instance;

        var allListings = new List<Listing>();
        var perPortalCounts = new Dictionary<string, (int fetched, string? error)>();

        foreach (var portal in enabled)
        {
            var adapter = AdapterFactory.Create(portal, http, logger, imports);
            if (adapter is null)
            {
                AnsiConsole.MarkupLine($"[yellow]{portal.Name}[/] — adapter type '{portal.Type}' not available in this phase; skipping.");
                perPortalCounts[portal.Name] = (0, $"adapter '{portal.Type}' not available");
                continue;
            }

            try
            {
                var fetched = await adapter.FetchAsync();
                var deduped = Deduper.Deduplicate(fetched);
                var stamp = DateTimeOffset.Now.ToString("yyyyMMdd");
                JsonReportWriter.WriteListings(deduped, Path.Combine(raw, $"{portal.Name}-{stamp}.json"));
                allListings.AddRange(deduped);
                perPortalCounts[portal.Name] = (deduped.Count, null);
                AnsiConsole.MarkupLine($"[green]✓[/] {portal.Name.EscapeMarkup()} — {deduped.Count} listing(s)");
            }
            catch (Exception ex)
            {
                perPortalCounts[portal.Name] = (0, ex.Message);
                AnsiConsole.MarkupLine($"[red]✗[/] {portal.Name.EscapeMarkup()} — {ex.GetType().Name.EscapeMarkup()}: {ex.Message.EscapeMarkup()}");
            }
        }

        var merged = Deduper.Deduplicate(allListings);
        JsonReportWriter.WriteListings(merged, Path.Combine(root, "data", "all_listings.json"));
        MarkdownReportWriter.WriteListings(merged, Path.Combine(root, "data", "top_jobs.md"),
            title: "Job listings (unranked — ranking arrives in Phase 4)");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary[/]");
        foreach (var (portal, (count, error)) in perPortalCounts)
        {
            var status = error is null ? $"[green]{count}[/]" : $"[red]error: {error.EscapeMarkup()}[/]";
            AnsiConsole.MarkupLine($"  {portal.EscapeMarkup()}: {status}");
        }
        AnsiConsole.MarkupLine($"  [bold]total after dedupe[/]: {merged.Count}");
        AnsiConsole.MarkupLine($"[dim]Wrote data/all_listings.json and data/top_jobs.md[/]");

        return 0;
    }
}
