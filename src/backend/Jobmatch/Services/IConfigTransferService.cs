using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Jobmatch.Json;

namespace Jobmatch.Services;

public sealed record ConfigExportManifest(
    int SchemaVersion,
    string Email,
    string ToolVersion,
    DateTimeOffset ExportedAt);

public sealed record ConfigImportResult(
    int Restored,
    int Skipped,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Exports the active user's complete on-disk state (everything under <c>data/&lt;email&gt;/</c>) as a
/// single zip archive, and restores such an archive back into the active user's directory. The LLM
/// model and transient files are excluded from exports; imports back up the current state before
/// overwriting so a bad archive is always recoverable.
/// </summary>
public interface IConfigTransferService
{
    byte[] Export();
    ConfigImportResult Import(Stream archive);
}

public sealed class ConfigTransferService(UserContext ctx) : IConfigTransferService
{
    public const int SchemaVersion = 1;
    private const string ManifestEntryName = "manifest.json";

    public byte[] Export()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in EnumerateExportableFiles())
            {
                var relative = ToArchivePath(Path.GetRelativePath(ctx.RootDir, file));
                var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var source = File.OpenRead(file);
                source.CopyTo(entryStream);
            }

            WriteManifest(zip);
        }

        return buffer.ToArray();
    }

    public ConfigImportResult Import(Stream archive)
    {
        // Copy to a seekable buffer: an upload stream may not support the random access ZipArchive needs.
        using var buffer = new MemoryStream();
        archive.CopyTo(buffer);
        buffer.Position = 0;

        using var zip = OpenArchive(buffer);

        var manifestEntry = zip.GetEntry(ManifestEntryName)
            ?? throw new InvalidRequestException(
                "This file is not a jobfinder export (no manifest.json inside the archive).");
        ValidateManifest(manifestEntry);

        var rootFull = Path.GetFullPath(ctx.RootDir);
        var restored = 0;
        var skipped = 0;
        var warnings = new List<string>();

        BackupCurrentState();

        foreach (var entry in zip.Entries)
        {
            if (string.Equals(entry.FullName, ManifestEntryName, StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            // Directory entries (name ending in '/') carry no content; created implicitly on extract.
            if (entry.FullName.EndsWith('/'))
                continue;

            // Never overwrite transient/locked state (live job queue, runtime logs, the model) with a
            // transported copy — raw/older archives may carry these.
            var entrySegments = entry.FullName.Split('/', '\\');
            if (entrySegments.Any(IsTransientDir) || IsHangfireDbFile(entrySegments[^1]))
            {
                skipped++;
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(ctx.RootDir, entry.FullName));
            if (!destination.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                skipped++;
                warnings.Add($"Skipped entry outside the data directory: {entry.FullName}");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            restored++;
        }

        return new ConfigImportResult(restored, skipped, warnings);
    }

    private static ZipArchive OpenArchive(Stream stream)
    {
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new InvalidRequestException("This file is not a valid zip archive.");
        }
    }

    private IEnumerable<string> EnumerateExportableFiles()
    {
        if (!Directory.Exists(ctx.RootDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(ctx.RootDir, "*", SearchOption.AllDirectories))
        {
            if (IsExcluded(Path.GetRelativePath(ctx.RootDir, file)))
                continue;
            yield return file;
        }
    }

    private static bool IsExcluded(string relativePath)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (segments.Any(IsTransientDir))
            return true;

        var name = segments[^1];
        return name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".download", StringComparison.OrdinalIgnoreCase)
            || name.Equals("portals.yml.bak", StringComparison.OrdinalIgnoreCase)
            || IsHangfireDbFile(name);
    }

    // Directories that are transient/regenerable or held open by the running app: the multi-GB
    // re-downloadable LLM model, the WARN+ runtime logs (host.log is locked while running), and
    // prior-import .backup-* folders. Excluded from exports and left in place across an import (never
    // moved to backup or overwritten). The user's search history lives in jobsearch/ + history/.
    private static bool IsTransientDir(string segment) =>
        segment.Equals("models", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("logs", StringComparison.OrdinalIgnoreCase)
        || segment.StartsWith(".backup-", StringComparison.Ordinal);

    // Hangfire's local job queue (hangfire.db + its -wal/-shm sidecars) — a transient root-level file
    // held open by the running app, so it is neither exported nor moved/overwritten during an import.
    private static bool IsHangfireDbFile(string name) =>
        name.StartsWith("hangfire.db", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moves the current data-directory contents into a timestamped <c>.backup-*</c> folder before an
    /// import overwrites them. Transient/locked state is deliberately left in place: the LLM model (so
    /// an import never forces a multi-GB re-download), the WARN+ runtime <c>logs/</c> and the Hangfire
    /// queue (<c>hangfire.db*</c>) — both held open by the running app, so moving them would throw —
    /// and earlier <c>.backup-*</c> folders.
    /// </summary>
    private void BackupCurrentState()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N")[..6];
        var backupDir = Path.Combine(ctx.RootDir, $".backup-{stamp}");
        Directory.CreateDirectory(backupDir);

        foreach (var dir in Directory.EnumerateDirectories(ctx.RootDir))
        {
            var name = Path.GetFileName(dir);
            if (IsTransientDir(name))
                continue; // leave the model, live logs, and prior backups in place — moving logs would throw
            Directory.Move(dir, Path.Combine(backupDir, name));
        }

        foreach (var file in Directory.EnumerateFiles(ctx.RootDir))
        {
            var name = Path.GetFileName(file);
            if (IsHangfireDbFile(name))
                continue; // leave the live (possibly locked) job queue in place — moving it would throw
            File.Move(file, Path.Combine(backupDir, name));
        }
    }

    private void WriteManifest(ZipArchive zip)
    {
        var manifest = new ConfigExportManifest(
            SchemaVersion,
            ctx.Email,
            ResolveToolVersion(),
            DateTimeOffset.UtcNow);

        var entry = zip.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, manifest, JobmatchJsonOptions.Indented);
    }

    private static void ValidateManifest(ZipArchiveEntry manifestEntry)
    {
        ConfigExportManifest? manifest;
        try
        {
            using var stream = manifestEntry.Open();
            manifest = JsonSerializer.Deserialize<ConfigExportManifest>(stream, JobmatchJsonOptions.Default);
        }
        catch (JsonException)
        {
            throw new InvalidRequestException("The export's manifest.json is corrupt or unreadable.");
        }

        if (manifest is null)
            throw new InvalidRequestException("The export's manifest.json is corrupt or unreadable.");

        if (manifest.SchemaVersion > SchemaVersion)
            throw new InvalidRequestException(
                $"This export was created by a newer version of jobfinder (format v{manifest.SchemaVersion}). "
                + "Update jobfinder and try again.");
    }

    private static string ToArchivePath(string relativePath) =>
        relativePath.Replace(Path.DirectorySeparatorChar, '/');

    private static string ResolveToolVersion()
    {
        var entry = Assembly.GetEntryAssembly();
        return entry?.GetName().Version?.ToString(3)
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "unknown";
    }
}
