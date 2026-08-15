namespace Jobmatch.Infrastructure.Paths;

/// <summary>
/// Where the user's data directory lives, before any of the files inside it exist. Two callers need
/// this answer earlier than <see cref="UserContext"/> can give it — the host's log directory and
/// Hangfire's job queue are both opened before first-run setup has chosen a location — so the
/// unconfigured fallback lives here rather than being written out at each of those call sites.
/// </summary>
public static class DataRoot
{
    /// <summary>
    /// Walks up from <paramref name="startDir"/> for a <c>.git</c> anchor and returns the repo root.
    /// Outside a checkout there is nothing stable to anchor to, so fall back to the per-user
    /// application-data location rather than the current directory, which moves with the shell.
    /// </summary>
    public static string FindRepoRootOrFallback(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Fallback()
            ?? throw new ConfigException(
                "Could not resolve a stable data directory: no `.git` anchor was found above "
                + $"'{startDir}', and Environment.SpecialFolder.LocalApplicationData returned empty. "
                + "Run jobfinder from inside a git repo, or set JOBFINDER_USER and ensure a writable "
                + "user-profile directory is available.");
    }

    /// <summary>
    /// The per-user location to write to when the active user cannot be resolved at all — no
    /// bootstrap config, no git identity, no <c>JOBFINDER_USER</c>. Null only if the platform
    /// reports no application-data directory.
    /// </summary>
    public static string? Fallback()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "jobfinder");
    }

    /// <summary>The fallback, created on disk and guaranteed non-null — for callers that must write somewhere.</summary>
    public static string EnsureFallback()
    {
        var path = Fallback()
            ?? throw new ConfigException(
                "No writable per-user application-data directory is available on this machine.");
        Directory.CreateDirectory(path);
        return path;
    }
}
