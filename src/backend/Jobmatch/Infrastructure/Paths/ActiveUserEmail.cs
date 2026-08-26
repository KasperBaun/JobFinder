using System.Diagnostics;

namespace Jobmatch.Infrastructure.Paths;

/// <summary>
/// Who the active user is. Resolution order is explicit override → <c>JOBFINDER_USER</c> →
/// <c>git config user.email</c>; the git lookup is a subprocess, which is why it is isolated here
/// rather than sitting inside the path layout.
/// </summary>
public static class ActiveUserEmail
{
    public const string EnvironmentVariable = "JOBFINDER_USER";

    public static string? TryResolve(string? emailOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(emailOverride))
            return emailOverride.Trim();

        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        return FromGitConfig();
    }

    public static string Resolve(string? emailOverride = null) =>
        TryResolve(emailOverride)
        ?? throw new ConfigException(
            "Could not determine the active user's email. Tried (in order): "
            + $"explicit override, environment variable {EnvironmentVariable}, and `git config user.email`. "
            + "Set one of these and try again.");

    private static string? FromGitConfig()
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
            // git missing, not a repo, or any other failure — the caller decides whether that is fatal.
            return null;
        }
    }
}
