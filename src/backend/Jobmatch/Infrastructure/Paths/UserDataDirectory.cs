using Microsoft.Extensions.Logging;

namespace Jobmatch.Infrastructure.Paths;

/// <summary>
/// Creating the user's data directory: the standard subdirectories, and the first-run seed of
/// <c>skillset.md</c> from the shipped example. Separate from <see cref="UserContext"/> because
/// resolving where files live and writing to disk are different things — a caller that only wants
/// a path should not create directories as a side effect.
/// </summary>
public static class UserDataDirectory
{
    internal static readonly string[] Subdirectories = ["imports", "raw", "history", "jobsearch", "examples"];

    /// <summary>Creates <paramref name="rootDir"/> and its subdirectories. Returns true if the root did not exist.</summary>
    public static bool Ensure(string rootDir)
    {
        var firstRun = !Directory.Exists(rootDir);
        Directory.CreateDirectory(rootDir);
        foreach (var sub in Subdirectories)
            Directory.CreateDirectory(Path.Combine(rootDir, sub));
        return firstRun;
    }

    /// <summary>
    /// Copies the shipped <c>skillset.example.md</c> into a freshly created directory, so a new user
    /// opens a template rather than an empty form. Never overwrites an existing profile.
    /// </summary>
    public static void SeedFromExamples(string rootDir, ILogger? logger = null)
    {
        var skillsetExample = Path.Combine(AppContext.BaseDirectory, "config", "skillset.example.md");
        if (!File.Exists(skillsetExample))
            return;

        var destination = Path.Combine(rootDir, "skillset.md");
        if (File.Exists(destination))
            return;

        File.Copy(skillsetExample, destination);
        logger?.LogInformation("Seeded {Path} from the shipped example profile", destination);
    }
}
