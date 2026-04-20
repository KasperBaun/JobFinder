using System.ComponentModel;
using Jobmatch;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Deduplication;
using Jobmatch.Models;
using Jobmatch.Output;
using Jobmatch.Ranking;
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

        if (!TryLoadConfigs(root, out var portals, out var skillset, out var ranking, out var error))
        {
            AnsiConsole.MarkupLine($"[red]{error.EscapeMarkup()}[/]");
            return 1;
        }

        var enabled = portals!
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
        var perPortal = new Dictionary<string, (int fetched, string? error)>();

        foreach (var portal in enabled)
        {
            var adapter = AdapterFactory.Create(portal, http, logger, imports);
            if (adapter is null)
            {
                AnsiConsole.MarkupLine($"[yellow]{portal.Name.EscapeMarkup()}[/] — adapter type '{portal.Type}' not available in this phase; skipping.");
                perPortal[portal.Name] = (0, $"adapter '{portal.Type}' not available");
                continue;
            }

            try
            {
                var fetched = await adapter.FetchAsync();
                var deduped = Deduper.Deduplicate(fetched);
                var stamp = DateTimeOffset.Now.ToString("yyyyMMdd");
                JsonReportWriter.WriteListings(deduped, Path.Combine(raw, $"{portal.Name}-{stamp}.json"));
                allListings.AddRange(deduped);
                perPortal[portal.Name] = (deduped.Count, null);
                AnsiConsole.MarkupLine($"[green]✓[/] {portal.Name.EscapeMarkup()} — {deduped.Count} listing(s)");
            }
            catch (Exception ex)
            {
                perPortal[portal.Name] = (0, ex.Message);
                AnsiConsole.MarkupLine($"[red]✗[/] {portal.Name.EscapeMarkup()} — {ex.GetType().Name.EscapeMarkup()}: {ex.Message.EscapeMarkup()}");
            }
        }

        var merged = Deduper.Deduplicate(allListings);
        JsonReportWriter.WriteListings(merged, Path.Combine(root, "data", "all_listings.json"));

        var rankingCfg = ranking!;
        if (settings.Top is int t and > 0)
        {
            rankingCfg = rankingCfg with { TopN = t };
        }

        var matches = Ranker.Rank(merged, skillset!, rankingCfg);
        JsonReportWriter.WriteMatches(matches, Path.Combine(root, "data", "ranked_listings.json"));
        MarkdownReportWriter.WriteMatches(matches, Path.Combine(root, "data", "top_jobs.md"),
            title: $"Top {matches.Count} job matches for {skillset!.Name.EscapeMarkup()}");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary[/]");
        foreach (var (portal, (count, err)) in perPortal)
        {
            var status = err is null ? $"[green]{count}[/]" : $"[red]error: {err.EscapeMarkup()}[/]";
            AnsiConsole.MarkupLine($"  {portal.EscapeMarkup()}: {status}");
        }
        AnsiConsole.MarkupLine($"  [bold]after dedupe[/]: {merged.Count}");
        AnsiConsole.MarkupLine($"  [bold]shortlisted[/]: {matches.Count} (min score {rankingCfg.MinScoreToInclude:0.00}, top {rankingCfg.TopN})");
        if (matches.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [bold]top score[/]: {matches[0].Score:0.00}");
        }
        AnsiConsole.MarkupLine("[dim]Wrote data/all_listings.json, data/ranked_listings.json, data/top_jobs.md[/]");

        return 0;
    }

    private static bool TryLoadConfigs(
        string root,
        out IReadOnlyList<PortalConfig>? portals,
        out Skillset? skillset,
        out RankingConfig? ranking,
        out string error)
    {
        portals = null;
        skillset = null;
        ranking = null;
        error = string.Empty;

        var skillsetPath = Path.Combine(root, "config", "skillset.md");
        var portalsPath = Path.Combine(root, "config", "portals.yml");
        var rankingPath = Path.Combine(root, "config", "ranking.yml");

        foreach (var (path, label) in new[] { (skillsetPath, "skillset.md"), (portalsPath, "portals.yml"), (rankingPath, "ranking.yml") })
        {
            if (!File.Exists(path))
            {
                error = $"Missing config/{label}. Copy config/{label}.example (if applicable) and edit.";
                return false;
            }
        }

        try
        {
            portals = PortalConfigLoader.Load(portalsPath);
            skillset = SkillsetParser.Load(skillsetPath);
            ranking = RankingConfigLoader.Load(rankingPath);
            return true;
        }
        catch (ConfigException ex)
        {
            error = $"Config error: {ex.Message}";
            return false;
        }
    }
}
