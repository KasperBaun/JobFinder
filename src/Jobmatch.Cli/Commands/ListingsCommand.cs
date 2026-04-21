using System.ComponentModel;
using System.Globalization;
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

        [Description("Override min_score_to_include from ranking.yml (0.0–1.0).")]
        [CommandOption("--min-score <SCORE>")]
        public double? MinScore { get; init; }

        [Description("Print a full score breakdown for the given listing URL (even if filtered out).")]
        [CommandOption("--explain <URL>")]
        public string? Explain { get; init; }
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
                var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                JsonReportWriter.WriteListings(fetched, Path.Combine(raw, $"{portal.Name}-{stamp}.json"));
                var deduped = Deduper.Deduplicate(fetched);
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
        if (settings.MinScore is double ms)
        {
            if (ms < 0.0 || ms > 1.0)
            {
                AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "[yellow]--min-score {0:0.00} is outside 0.0–1.0; ignoring and using {1:0.00} from ranking.yml[/]",
                    ms, rankingCfg.MinScoreToInclude));
            }
            else
            {
                rankingCfg = rankingCfg with { MinScoreToInclude = ms };
            }
        }

        var scored = Ranker.Score(merged, skillset!, rankingCfg);
        var matches = Ranker.Filter(scored, rankingCfg);
        JsonReportWriter.WriteMatches(matches, Path.Combine(root, "data", "ranked_listings.json"));
        MarkdownReportWriter.WriteMatches(matches, Path.Combine(root, "data", "top_jobs.md"),
            title: $"Top {matches.Count} job matches for {skillset!.Name}");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary[/]");
        foreach (var (portal, (count, err)) in perPortal)
        {
            var status = err is null ? $"[green]{count}[/]" : $"[red]error: {err.EscapeMarkup()}[/]";
            AnsiConsole.MarkupLine($"  {portal.EscapeMarkup()}: {status}");
        }
        AnsiConsole.MarkupLine($"  [bold]after dedupe[/]: {merged.Count}");

        var disqualifiers = CountDisqualifiers(scored);
        if (disqualifiers.Count > 0)
        {
            var breakdown = string.Join(", ", disqualifiers.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key.EscapeMarkup()}×{kv.Value}"));
            AnsiConsole.MarkupLine($"  [bold]disqualified[/]: {disqualifiers.Values.Sum()} ({breakdown})");
        }

        var filtered = scored.Count(m => m.Reasoning.DisqualifierHits.Count == 0 && m.Score < rankingCfg.MinScoreToInclude);
        AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
            "  [bold]shortlisted[/]: {0} (min score {1:0.00}, top {2})",
            matches.Count, rankingCfg.MinScoreToInclude, rankingCfg.TopN));
        if (matches.Count == 0 && filtered > 0)
        {
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "  [yellow]hint[/]: {0} listing(s) fell below the threshold — try [yellow]--min-score {1:0.00}[/] to see more",
                filtered, Math.Max(0.05, rankingCfg.MinScoreToInclude - 0.10)));
        }
        if (matches.Count > 0)
        {
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "  [bold]top score[/]: {0:0.00}", matches[0].Score));
        }
        AnsiConsole.MarkupLine("[dim]Wrote data/all_listings.json, data/ranked_listings.json, data/top_jobs.md[/]");

        if (!string.IsNullOrWhiteSpace(settings.Explain))
        {
            ExplainListing(settings.Explain, scored, rankingCfg);
        }

        return 0;
    }

    private static void ExplainListing(string urlInput, IReadOnlyList<Jobmatch.Models.Match> scored, RankingConfig rankingCfg)
    {
        AnsiConsole.WriteLine();
        if (!Uri.TryCreate(urlInput, UriKind.Absolute, out var target))
        {
            AnsiConsole.MarkupLine($"[red]--explain:[/] '{urlInput.EscapeMarkup()}' is not a valid absolute URL.");
            return;
        }

        var targetKey = Deduper.NormaliseUrl(target);
        var match = scored.FirstOrDefault(m => Deduper.NormaliseUrl(m.Listing.Url) == targetKey);
        if (match is null)
        {
            AnsiConsole.MarkupLine($"[yellow]--explain:[/] {urlInput.EscapeMarkup()} was not in the fetched listings.");
            var sample = scored.Take(5).Select(m => m.Listing.Url.ToString()).ToList();
            if (sample.Count > 0)
            {
                AnsiConsole.MarkupLine("[dim]First few URLs that were fetched:[/]");
                foreach (var u in sample)
                {
                    AnsiConsole.MarkupLine($"  • {u.EscapeMarkup()}");
                }
            }
            return;
        }

        var l = match.Listing;
        var includedState = match.Reasoning.DisqualifierHits.Count > 0
            ? "[red]DROPPED (disqualified)[/]"
            : match.Score < rankingCfg.MinScoreToInclude
                ? $"[yellow]DROPPED (below min score {rankingCfg.MinScoreToInclude.ToString("0.00", CultureInfo.InvariantCulture)})[/]"
                : "[green]INCLUDED[/]";

        AnsiConsole.MarkupLine($"[bold cyan]Explain:[/] {l.Title.EscapeMarkup()} [dim](via {l.Portal.EscapeMarkup()})[/]");
        AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [bold]score[/]: {0:0.000}  {1}", match.Score, includedState));
        AnsiConsole.MarkupLine("  [bold]signals[/]:");
        foreach (var kv in match.Breakdown.OrderByDescending(k => k.Value))
        {
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "    {0,-16} {1:0.000}", kv.Key, kv.Value));
        }
        AnsiConsole.MarkupLine($"  [bold]reasoning[/]: {match.Reasoning.Notes.EscapeMarkup()}");
        PrintHitList("primary stack", match.Reasoning.PrimaryStackHits);
        PrintHitList("secondary stack", match.Reasoning.SecondaryStackHits);
        PrintHitList("domains", match.Reasoning.DomainHits);
        PrintHitList("disqualifiers", match.Reasoning.DisqualifierHits);
    }

    private static void PrintHitList(string label, IReadOnlyList<string> hits)
    {
        if (hits.Count == 0) return;
        AnsiConsole.MarkupLine($"    {label.EscapeMarkup()}: {string.Join(", ", hits).EscapeMarkup()}");
    }

    private static Dictionary<string, int> CountDisqualifiers(IReadOnlyList<Jobmatch.Models.Match> scored)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in scored)
        {
            foreach (var dq in m.Reasoning.DisqualifierHits)
            {
                counts[dq] = counts.GetValueOrDefault(dq) + 1;
            }
        }
        return counts;
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

        foreach (var (path, label, hasExample) in new[]
        {
            (skillsetPath, "skillset.md", true),
            (portalsPath, "portals.yml", true),
            (rankingPath, "ranking.yml", false),
        })
        {
            if (!File.Exists(path))
            {
                error = hasExample
                    ? $"Missing config/{label}. Copy config/{label.Replace(".md", ".example.md").Replace(".yml", ".example.yml")} and edit."
                    : $"Missing config/{label}. Restore it from version control or regenerate it from the PRD defaults.";
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
