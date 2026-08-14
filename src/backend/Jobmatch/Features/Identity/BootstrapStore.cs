using System.Text.Json;
using Jobmatch.Platform.IO;
using Jobmatch.Platform.Json;

namespace Jobmatch.Features.Identity;

/// <summary>
/// The one setting the app persists <em>outside</em> the user's data directory: which directory that
/// is, who the active user is, and the interface language. Recorded on first-run setup after the user
/// confirms the location. <see cref="Language"/> lives here rather than under
/// <c>data/&lt;email&gt;/</c> because the setup wizard needs it before a data directory exists.
/// </summary>
public sealed record BootstrapConfig(
    string Email,
    string DataDir,
    DateTimeOffset AcknowledgedAt,
    string? Language = null);

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
        AtomicFile.Write(Path, stream => JsonSerializer.Serialize(stream, config, JobmatchJsonOptions.Indented));
    }
}
