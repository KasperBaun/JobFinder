using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Verification;

public sealed class ConfigVerifier
{
    private readonly string _root;
    private readonly HttpClient? _http;

    public ConfigVerifier(string? root = null, HttpClient? http = null)
    {
        _root = root ?? Directory.GetCurrentDirectory();
        _http = http;
    }

    private string ConfigPath(string filename) => Path.Combine(_root, "config", filename);
    private string ImportsPath => Path.Combine(_root, "data", "imports");

    public async Task<VerificationReport> VerifyAsync(bool includeConnectivity = false, CancellationToken ct = default)
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

        if (includeConnectivity)
        {
            checks.AddRange(await CheckConnectivityAsync(ct));
        }

        return new VerificationReport(checks);
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
            return new VerificationCheck("Skillset parses", VerificationStatus.Fail, $"unexpected error: {ex.Message}");
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
                if (p.Type is PortalType.Html && (p.Endpoint is null || p.Html is null))
                {
                    problems.Add($"'{p.Name}' (html): requires both 'endpoint' and an 'html' selector block");
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
            return new VerificationCheck("Portal config parses", VerificationStatus.Fail, $"unexpected error: {ex.Message}");
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
            return new VerificationCheck("Ranking weights valid", VerificationStatus.Fail, $"unexpected error: {ex.Message}");
        }
    }

    private VerificationCheck CheckManualImports()
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("Manual import files present", VerificationStatus.Warn, "portals.yml missing; skipped");
        }
        try
        {
            var portals = PortalConfigLoader.Load(path);
            var manualEnabled = portals.Where(p => p.Enabled && p.Type == PortalType.Manual).ToList();
            if (manualEnabled.Count == 0)
            {
                return new VerificationCheck("Manual import files present", VerificationStatus.Pass, "no manual portals enabled");
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
            return new VerificationCheck("Manual import files present", VerificationStatus.Warn, "portals.yml could not be parsed; skipped");
        }
    }

    private VerificationCheck CheckHtmlAdapterAvailable()
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn, "portals.yml missing; skipped");
        }
        try
        {
            var portals = PortalConfigLoader.Load(path);
            var htmlEnabled = portals.Where(p => p.Enabled && p.Type == PortalType.Html).ToList();
            if (htmlEnabled.Count == 0)
            {
                return new VerificationCheck("HTML adapter available", VerificationStatus.Pass, "no html portals enabled");
            }
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn,
                $"{htmlEnabled.Count} html portal(s) enabled — ensure Playwright browsers are installed: {HtmlAdapter.PlaywrightInstallCommand}");
        }
        catch (ConfigException)
        {
            return new VerificationCheck("HTML adapter available", VerificationStatus.Warn, "portals.yml could not be parsed; skipped");
        }
    }

    private async Task<IReadOnlyList<VerificationCheck>> CheckConnectivityAsync(CancellationToken ct)
    {
        var path = ConfigPath("portals.yml");
        if (!File.Exists(path))
        {
            return [new VerificationCheck("Connectivity", VerificationStatus.Warn, "portals.yml missing; skipped")];
        }

        IReadOnlyList<PortalConfig> portals;
        try
        {
            portals = PortalConfigLoader.Load(path);
        }
        catch (ConfigException)
        {
            return [new VerificationCheck("Connectivity", VerificationStatus.Warn, "portals.yml could not be parsed; skipped")];
        }

        var testable = portals.Where(p => p.Enabled && p.Endpoint is not null &&
                                           p.Type is PortalType.Api or PortalType.Rss).ToList();
        if (testable.Count == 0)
        {
            return [new VerificationCheck("Connectivity", VerificationStatus.Pass, "no api/rss portals enabled")];
        }

        var ownedClient = _http is null;
        var http = _http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var results = new List<VerificationCheck>();
            foreach (var portal in testable)
            {
                results.Add(await ProbeAsync(http, portal, ct));
            }
            return results;
        }
        finally
        {
            if (ownedClient) http.Dispose();
        }
    }

    private static async Task<VerificationCheck> ProbeAsync(HttpClient http, PortalConfig portal, CancellationToken ct)
    {
        var name = $"Connectivity: {portal.Name}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, portal.Endpoint);
            request.Headers.Accept.ParseAdd(portal.Type == PortalType.Rss ? "application/rss+xml, application/xml" : "application/json");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var code = (int)response.StatusCode;
            if (code >= 200 && code < 300)
            {
                return new VerificationCheck(name, VerificationStatus.Pass, $"HTTP {code}");
            }
            return new VerificationCheck(name, VerificationStatus.Warn, $"HTTP {code} {response.ReasonPhrase}");
        }
        catch (TaskCanceledException)
        {
            return new VerificationCheck(name, VerificationStatus.Warn, "timeout after 10s");
        }
        catch (HttpRequestException ex)
        {
            return new VerificationCheck(name, VerificationStatus.Warn, $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new VerificationCheck(name, VerificationStatus.Warn, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
