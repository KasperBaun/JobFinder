using Microsoft.Extensions.Logging;

namespace Jobmatch.Infrastructure.Paths;

/// <summary>
/// Every path the app is allowed to read or write for the active user, derived once from
/// <c>data/&lt;email&gt;/</c>. Nothing outside this type builds a path into the data directory.
/// </summary>
/// <remarks>
/// Resolution is deliberately split: <see cref="ActiveUserEmail"/> answers who the user is,
/// <see cref="DataRoot"/> answers where their directory lives, and <see cref="UserDataDirectory"/>
/// creates and seeds it. <see cref="Resolve"/> composes the three.
/// </remarks>
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
    public required string HistoryDir { get; init; }
    public required string JobSearchDir { get; init; }
    public required string MarksPath { get; init; }
    public required string ExamplesDir { get; init; }
    public required string ProviderStatePath { get; init; }
    public required string UserProvidersPath { get; init; }

    /// <summary>
    /// Resolves the active user, lays out their paths under <c>{repoRoot}/data/{email}/</c>, and
    /// creates the directory (seeding it on first run).
    /// </summary>
    /// <param name="emailOverride">Optional email override; takes precedence over env and git.</param>
    /// <param name="repoRoot">Optional repo root; defaults to walking up from cwd looking for <c>.git</c>, falling back to the per-user application-data directory if none is found.</param>
    /// <param name="seedExamples">When true (default), copy the example profile into a freshly created RootDir.</param>
    /// <param name="cwdOverride">Optional cwd override used as the start of the <c>.git</c> walk-up; defaults to <see cref="Directory.GetCurrentDirectory"/>.</param>
    /// <param name="dataDirOverride">Optional explicit data directory; when set it becomes <see cref="RootDir"/> verbatim (used once the user has chosen a location during first-run setup), bypassing the <c>{repoRoot}/data/{email}</c> layout.</param>
    /// <param name="logger">Optional logger for the first-run seed.</param>
    public static UserContext Resolve(
        string? emailOverride = null,
        string? repoRoot = null,
        bool seedExamples = true,
        string? cwdOverride = null,
        string? dataDirOverride = null,
        ILogger? logger = null)
    {
        var email = ActiveUserEmail.Resolve(emailOverride);

        var cwd = cwdOverride ?? Directory.GetCurrentDirectory();
        var rootDir = !string.IsNullOrWhiteSpace(dataDirOverride)
            ? Path.GetFullPath(dataDirOverride)
            : Path.Combine(repoRoot ?? DataRoot.FindRepoRootOrFallback(cwd), "data", email);

        if (UserDataDirectory.Ensure(rootDir) && seedExamples)
            UserDataDirectory.SeedFromExamples(rootDir, logger);

        return Layout(email, rootDir);
    }

    public static UserContext Layout(string email, string rootDir)
    {
        // A per-user ranking.yml wins; otherwise the shipped default is used in place, never copied,
        // so an upgrade's tuning reaches users who never customised theirs.
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
            ImportsDir = Path.Combine(rootDir, "imports"),
            RawDir = Path.Combine(rootDir, "raw"),
            AllListingsPath = Path.Combine(rootDir, "all-listings.json"),
            RankedListingsPath = Path.Combine(rootDir, "ranked-listings.json"),
            TopJobsPath = Path.Combine(rootDir, "top-jobs.md"),
            HistoryDir = Path.Combine(rootDir, "history"),
            JobSearchDir = Path.Combine(rootDir, "jobsearch"),
            MarksPath = Path.Combine(rootDir, "marks.json"),
            ExamplesDir = Path.Combine(rootDir, "examples"),
            ProviderStatePath = Path.Combine(rootDir, "provider-state.json"),
            UserProvidersPath = Path.Combine(rootDir, "user-providers.json"),
        };
    }

    /// <inheritdoc cref="ActiveUserEmail.TryResolve"/>
    public static string? TryResolveEmail(string? emailOverride = null) => ActiveUserEmail.TryResolve(emailOverride);

    /// <summary>
    /// The default data directory to <em>suggest</em> to the user during first-run setup:
    /// <c>{repoRoot|fallback}/data/{email}</c>. Used as a pre-filled hint only — the user
    /// confirms or changes it before anything is written.
    /// </summary>
    public static string SuggestDefaultDataDir(string? email, string? cwdOverride = null)
    {
        var cwd = cwdOverride ?? Directory.GetCurrentDirectory();
        var root = DataRoot.FindRepoRootOrFallback(cwd);
        var folder = string.IsNullOrWhiteSpace(email) ? "me" : email.Trim();
        return Path.Combine(root, "data", folder);
    }
}
