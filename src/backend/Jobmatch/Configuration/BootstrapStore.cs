using System.Text.Json;
using Jobmatch.Json;

namespace Jobmatch.Configuration;

/// <summary>
/// The one setting the app persists <em>outside</em> the user's data directory: which directory that
/// is, and who the active user is. Recorded on first-run setup after the user confirms the location.
/// </summary>
public sealed record BootstrapConfig(string Email, string DataDir, DateTimeOffset AcknowledgedAt);

/// <summary>
/// Reads and writes <see cref="BootstrapConfig"/> at a fixed per-user location that does not depend on
/// the (still-unknown) data directory: <c>{ApplicationData}/jobfinder/bootstrap.json</c>
/// (<c>%APPDATA%</c> on Windows, <c>~/.config</c> on Unix). The path is overridable for tests.
/// </summary>
public sealed class BootstrapStore
{
    public string Path { get; }

    public BootstrapStore(string? pathOverride = null)
    {
        Path = pathOverride
            ?? Environment.GetEnvironmentVariable("JOBFINDER_BOOTSTRAP")
            ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jobfinder",
                "bootstrap.json");
    }

    public BootstrapConfig? TryLoad()
    {
        try
        {
            if (!File.Exists(Path)) return null;
            using var stream = File.OpenRead(Path);
            var config = JsonSerializer.Deserialize<BootstrapConfig>(stream, JobmatchJsonOptions.Default);
            return string.IsNullOrWhiteSpace(config?.Email) || string.IsNullOrWhiteSpace(config?.DataDir)
                ? null
                : config;
        }
        catch
        {
            return null;
        }
    }

    public void Save(BootstrapConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, config, JobmatchJsonOptions.Indented);
        }
        File.Move(tmp, Path, overwrite: true);
    }
}
