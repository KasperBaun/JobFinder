using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Verification;

public sealed class ConfigVerifier
{
    private readonly string _root;

    public ConfigVerifier(string? root = null)
    {
        _root = root ?? Directory.GetCurrentDirectory();
    }

    private string ConfigPath(string filename) => Path.Combine(_root, "config", filename);
    private string ImportsPath => Path.Combine(_root, "data", "imports");

    public Task<VerificationReport> VerifyAsync(bool includeConnectivity = false, CancellationToken ct = default)
    {
        var checks = new List<VerificationCheck>
        {
            CheckFilesPresent(),
            CheckSkillset(),
            CheckPortals(),
            CheckRanking(),
            CheckManualImports(),
            CheckHtmlAdapterAvailable(),
        };

        // Phase 5 adds connectivity checks; stub for now.
        if (includeConnectivity)
        {
            checks.Add(new VerificationCheck("Connectivity", VerificationStatus.Warn,
                "connectivity checks not yet implemented (Phase 5)."));
        }

        return Task.FromResult(new VerificationReport(checks));
    }

    private VerificationCheck CheckFilesPresent()
    {
        var required = new[] { "skillset.md", "portals.yml", "ranking.yml" };
        var missing = required.Where(f => !File.Exists(ConfigPath(f))).ToList();
        return missing.Count == 0
            ? new VerificationCheck("Required config files present", VerificationStatus.Pass,
                "skillset.md, portals.yml, ranking.yml all found under config/")
            : new VerificationCheck("Required config files present", VerificationStatus.Fail,
                $"missing: {string.Join(", ", missing.Select(m => "config/" + m))}");
    }

    private VerificationCheck CheckSkillset()
    {
        var path = ConfigPath("skillset.md");
        if (!File.Exists(path))
        {
            return new VerificationCheck("Skillset parses", VerificationStatus.Fail, "config/skillset.md not found");
        }
        try
        {
            var skillset = SkillsetParser.Load(path);
            if (skillset.PrimaryStack.Count == 0)
            {
                return new VerificationCheck("Skillset parses", VerificationStatus.Fail,
                    "primary stack is empty — at least one keyword is required");
            }
            return new VerificationCheck("Skillset parses", VerificationStatus.Pass,
                $"name='{skillset.Name}', primary stack has {skillset.PrimaryStack.Count} item(s)");
        }
        catch (ConfigException ex)
        {
            return new VerificationCheck("Skillset parses", VerificationStatus.Fail, ex.Message);
        }
        catch (Exception ex)
        {
            return new VerificationCheck("Skillset parses", VerificationStatus.Fail,
                $"unexpected error: {ex.Message}");
        }
    }

    private VerificationCheck CheckPortals()
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("Portal config parses", VerificationStatus.Fail, "config/portals.yml not found");
        }
        try
        {
            var portals = PortalConfigLoader.Load(path);
            var problems = new List<string>();
            foreach (var p in portals.Where(p => p.Enabled))
            {
                if (p.Type is PortalType.Api or PortalType.Rss && p.Endpoint is null)
                {
                    problems.Add($"'{p.Name}' ({p.Type}): missing required 'endpoint'");
                }
            }
            if (problems.Count > 0)
            {
                return new VerificationCheck("Portal config parses", VerificationStatus.Fail, string.Join("; ", problems));
            }
            var enabledCount = portals.Count(p => p.Enabled);
            return new VerificationCheck("Portal config parses", VerificationStatus.Pass,
                $"{portals.Count} portal(s) defined, {enabledCount} enabled");
        }
        catch (ConfigException ex)
        {
            return new VerificationCheck("Portal config parses", VerificationStatus.Fail, ex.Message);
        }
        catch (Exception ex)
        {
            return new VerificationCheck("Portal config parses", VerificationStatus.Fail,
                $"unexpected error: {ex.Message}");
        }
    }

    private VerificationCheck CheckRanking()
    {
        var path = ConfigPath("ranking.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail, "config/ranking.yml not found");
        }
        try
        {
            var ranking = RankingConfigLoader.Load(path);
            var sum = ranking.Weights.Sum();
            if (Math.Abs(sum - 1.0) > 0.01)
            {
                return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail,
                    $"weights sum to {sum:0.###}, expected 1.0 ± 0.01");
            }
            if (ranking.TopN < 1)
            {
                return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail,
                    $"top_n must be >= 1, got {ranking.TopN}");
            }
            return new VerificationCheck("Ranking weights valid", VerificationStatus.Pass,
                $"weights sum to {sum:0.###}, top_n={ranking.TopN}");
        }
        catch (ConfigException ex)
        {
            return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail, ex.Message);
        }
        catch (Exception ex)
        {
            return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail,
                $"unexpected error: {ex.Message}");
        }
    }

    private VerificationCheck CheckManualImports()
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("Manual import files present", VerificationStatus.Warn,
                "portals.yml missing; skipped");
        }
        try
        {
            var portals = PortalConfigLoader.Load(path);
            var manualEnabled = portals.Where(p => p.Enabled && p.Type == PortalType.Manual).ToList();
            if (manualEnabled.Count == 0)
            {
                return new VerificationCheck("Manual import files present", VerificationStatus.Pass,
                    "no manual portals enabled");
            }
            if (!Directory.Exists(ImportsPath))
            {
                return new VerificationCheck("Manual import files present", VerificationStatus.Warn,
                    $"data/imports/ does not exist; create it and drop exports for: {string.Join(", ", manualEnabled.Select(p => p.Name))}");
            }
            var missing = manualEnabled
                .Where(p => !Directory.EnumerateFiles(ImportsPath, $"{p.Name}-*.*").Any())
                .Select(p => p.Name)
                .ToList();
            return missing.Count == 0
                ? new VerificationCheck("Manual import files present", VerificationStatus.Pass,
                    $"import files found for {manualEnabled.Count} manual portal(s)")
                : new VerificationCheck("Manual import files present", VerificationStatus.Warn,
                    $"no import files for: {string.Join(", ", missing)} (expected data/imports/<portal>-*.csv or .json)");
        }
        catch (ConfigException)
        {
            return new VerificationCheck("Manual import files present", VerificationStatus.Warn,
                "portals.yml could not be parsed; skipped");
        }
    }

    private VerificationCheck CheckHtmlAdapterAvailable()
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn,
                "portals.yml missing; skipped");
        }
        try
        {
            var portals = PortalConfigLoader.Load(path);
            var htmlEnabled = portals.Any(p => p.Enabled && p.Type == PortalType.Html);
            if (!htmlEnabled)
            {
                return new VerificationCheck("HTML adapter available", VerificationStatus.Pass,
                    "no html portals enabled");
            }
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn,
                "html portals enabled but the HTML adapter lands in Phase 5 — those portals will be skipped until then");
        }
        catch (ConfigException)
        {
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn,
                "portals.yml could not be parsed; skipped");
        }
    }
}
