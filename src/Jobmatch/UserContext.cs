using System.Diagnostics;

namespace Jobmatch;

/// <summary>
/// Resolves the active user (by email) and the on-disk paths derived from <c>data/&lt;email&gt;/</c>.
/// On first run for a user, seeds the user's data directory with example skillset and portals
/// templates copied from <c>{AppContext.BaseDirectory}/config/</c>.
/// </summary>
public sealed class UserContext
{
    public required string Email { get; init; }
    public required string RootDir { get; init; }
    public required string SkillsetPath { get; init; }
    public required string PortalsPath { get; init; }
    public required string RankingPath { get; init; }
    public required string ImportsDir { get; init; }
    public required string RawDir { get; init; }
    public required string AllListingsPath { get; init; }
    public required string RankedListingsPath { get; init; }
    public required string TopJobsPath { get; init; }
    public required string VerificationReportPath { get; init; }
    public required string HistoryDir { get; init; }
    public required string MarksPath { get; init; }
    public required string ExamplesDir { get; init; }

    /// <summary>
    /// Resolves the active <see cref="UserContext"/> by determining the email, building the on-disk
    /// path layout under <c>{repoRoot}/data/{email}/</c>, performing first-run seeding if the
    /// directory doesn't yet exist, and ensuring the standard subdirectories exist.
    /// </summary>
    /// <param name="emailOverride">Optional email override; takes precedence over env and git.</param>
    /// <param name="repoRoot">Optional repo root; defaults to <see cref="Directory.GetCurrentDirectory"/>.</param>
    /// <param name="seedExamples">When true (default), copy example configs into a freshly created RootDir.</param>
    public static UserContext Resolve(
        string? emailOverride = null,
        string? repoRoot = null,
        bool seedExamples = true)
    {
        var email = ResolveEmail(emailOverride)
            ?? throw new ConfigException(
                "Could not determine the active user's email. Tried (in order): "
                + "explicit override, environment variable JOBFINDER_USER, and `git config user.email`. "
                + "Set one of these and try again.");

        var root = repoRoot ?? Directory.GetCurrentDirectory();
        var rootDir = Path.Combine(root, "data", email);

        var firstRun = !Directory.Exists(rootDir);
        if (firstRun)
        {
            Directory.CreateDirectory(rootDir);
            if (seedExamples)
            {
                SeedFromExamples(rootDir);
            }
        }

        var importsDir = Path.Combine(rootDir, "imports");
        var rawDir = Path.Combine(rootDir, "raw");
        var historyDir = Path.Combine(rootDir, "history");
        var examplesDir = Path.Combine(rootDir, "examples");

        Directory.CreateDirectory(importsDir);
        Directory.CreateDirectory(rawDir);
        Directory.CreateDirectory(historyDir);
        Directory.CreateDirectory(examplesDir);

        var userRanking = Path.Combine(rootDir, "ranking.yml");
        var rankingPath = File.Exists(userRanking)
            ? userRanking
            : Path.Combine(AppContext.BaseDirectory, "config", "ranking.yml");

        return new UserContext
        {
            Email = email,
            RootDir = rootDir,
            SkillsetPath = Path.Combine(rootDir, "skillset.md"),
            PortalsPath = Path.Combine(rootDir, "portals.yml"),
            RankingPath = rankingPath,
            ImportsDir = importsDir,
            RawDir = rawDir,
            AllListingsPath = Path.Combine(rootDir, "all-listings.json"),
            RankedListingsPath = Path.Combine(rootDir, "ranked-listings.json"),
            TopJobsPath = Path.Combine(rootDir, "top-jobs.md"),
            VerificationReportPath = Path.Combine(rootDir, "verification-report.md"),
            HistoryDir = historyDir,
            MarksPath = Path.Combine(rootDir, "marks.json"),
            ExamplesDir = examplesDir,
        };
    }

    private static string? ResolveEmail(string? emailOverride)
    {
        if (!string.IsNullOrWhiteSpace(emailOverride))
            return emailOverride.Trim();

        var env = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        return TryGetGitUserEmail();
    }

    private static string? TryGetGitUserEmail()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "config user.email")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                return null;

            var trimmed = output.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
        catch
        {
            // git missing, not a repo, or any other failure → fall through to ConfigException
            return null;
        }
    }

    private static void SeedFromExamples(string rootDir)
    {
        var examplesDir = Path.Combine(AppContext.BaseDirectory, "config");
        var skillsetExample = Path.Combine(examplesDir, "skillset.example.md");
        var portalsExample = Path.Combine(examplesDir, "portals.example.yml");

        var seededSkillset = false;
        var seededPortals = false;

        if (File.Exists(skillsetExample))
        {
            File.Copy(skillsetExample, Path.Combine(rootDir, "skillset.md"), overwrite: false);
            seededSkillset = true;
        }

        if (File.Exists(portalsExample))
        {
            File.Copy(portalsExample, Path.Combine(rootDir, "portals.yml"), overwrite: false);
            seededPortals = true;
        }

        if (seededSkillset && seededPortals)
        {
            Console.WriteLine($"[first-run] seeded {rootDir}/skillset.md and portals.yml from examples");
        }
        else if (seededSkillset)
        {
            Console.WriteLine($"[first-run] seeded {rootDir}/skillset.md from examples");
        }
        else if (seededPortals)
        {
            Console.WriteLine($"[first-run] seeded {rootDir}/portals.yml from examples");
        }
    }
}
