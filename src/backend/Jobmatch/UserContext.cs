using System.Diagnostics;

namespace Jobmatch;

/// <summary>
/// Resolves the active user (by email) and the on-disk paths derived from <c>data/&lt;email&gt;/</c>.
/// On first run for a user, seeds the user's data directory with an example skillset template
/// copied from <c>{AppContext.BaseDirectory}/config/</c>.
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
    public required string JobSearchDir { get; init; }
    public required string MarksPath { get; init; }
    public required string ExamplesDir { get; init; }
    public required string ProviderStatePath { get; init; }

    /// <summary>
    /// Resolves the active <see cref="UserContext"/> by determining the email, building the on-disk
    /// path layout under <c>{repoRoot}/data/{email}/</c>, performing first-run seeding if the
    /// directory doesn't yet exist, and ensuring the standard subdirectories exist.
    /// </summary>
    /// <param name="emailOverride">Optional email override; takes precedence over env and git.</param>
    /// <param name="repoRoot">Optional repo root; defaults to walking up from cwd looking for <c>.git</c>, falling back to <c>%LOCALAPPDATA%/jobfinder</c> if none found.</param>
    /// <param name="seedExamples">When true (default), copy example configs into a freshly created RootDir.</param>
    /// <param name="cwdOverride">Optional cwd override used as the start of the <c>.git</c> walk-up; defaults to <see cref="Directory.GetCurrentDirectory"/>.</param>
    /// <param name="dataDirOverride">Optional explicit data directory; when set it becomes <see cref="RootDir"/> verbatim (used once the user has chosen a location during first-run setup), bypassing the <c>{repoRoot}/data/{email}</c> layout.</param>
    public static UserContext Resolve(
        string? emailOverride = null,
        string? repoRoot = null,
        bool seedExamples = true,
        string? cwdOverride = null,
        string? dataDirOverride = null)
    {
        var email = ResolveEmail(emailOverride)
            ?? throw new ConfigException(
                "Could not determine the active user's email. Tried (in order): "
                + "explicit override, environment variable JOBFINDER_USER, and `git config user.email`. "
                + "Set one of these and try again.");

        var cwd = cwdOverride ?? Directory.GetCurrentDirectory();
        var rootDir = !string.IsNullOrWhiteSpace(dataDirOverride)
            ? Path.GetFullPath(dataDirOverride)
            : Path.Combine(repoRoot ?? FindRepoRootOrStableFallback(cwd), "data", email);

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
        var jobSearchDir = Path.Combine(rootDir, "jobsearch");
        var examplesDir = Path.Combine(rootDir, "examples");

        Directory.CreateDirectory(importsDir);
        Directory.CreateDirectory(rawDir);
        Directory.CreateDirectory(historyDir);
        Directory.CreateDirectory(jobSearchDir);
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
            JobSearchDir = jobSearchDir,
            MarksPath = Path.Combine(rootDir, "marks.json"),
            ExamplesDir = examplesDir,
            ProviderStatePath = Path.Combine(rootDir, "provider-state.json"),
        };
    }

    private static string FindRepoRootOrStableFallback(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        // No .git anchor: fall back to a stable per-user location instead of cwd.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            throw new ConfigException(
                "Could not resolve a stable data directory: no `.git` anchor was found above "
                + $"'{startDir}', and Environment.SpecialFolder.LocalApplicationData returned empty. "
                + "Run jobfinder from inside a git repo, or set JOBFINDER_USER and ensure a writable "
                + "user-profile directory is available.");
        }
        return Path.Combine(localAppData, "jobfinder");
    }

    /// <summary>Best-effort email resolution (override → env → git), returning null instead of throwing.</summary>
    public static string? TryResolveEmail(string? emailOverride = null) => ResolveEmail(emailOverride);

    /// <summary>
    /// The default data directory to <em>suggest</em> to the user during first-run setup:
    /// <c>{repoRoot|stable-fallback}/data/{email}</c>. Used as a pre-filled hint only — the user
    /// confirms or changes it before anything is written.
    /// </summary>
    public static string SuggestDefaultDataDir(string? email, string? cwdOverride = null)
    {
        var cwd = cwdOverride ?? Directory.GetCurrentDirectory();
        var root = FindRepoRootOrStableFallback(cwd);
        var folder = string.IsNullOrWhiteSpace(email) ? "me" : email.Trim();
        return Path.Combine(root, "data", folder);
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

        if (File.Exists(skillsetExample))
        {
            File.Copy(skillsetExample, Path.Combine(rootDir, "skillset.md"), overwrite: false);
            Console.WriteLine($"[first-run] seeded {rootDir}/skillset.md from examples");
        }
    }
}
