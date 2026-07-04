using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Jobmatch;
using Jobmatch.Json;
using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

public sealed class ConfigTransferServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public ConfigTransferServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "cfg-transfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private UserContext NewCtx(string repoRoot, string email = "x@y") =>
        UserContext.Resolve(emailOverride: email, repoRoot: repoRoot, seedExamples: false);

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Export_IncludesUserData_And_Manifest()
    {
        var ctx = NewCtx(_tempRoot);
        Write(ctx.SkillsetPath, "# skills");
        Write(ctx.ProviderStatePath, "{\"secrets\":{}}");
        Write(Path.Combine(ctx.HistoryDir, "run1.json"), "{}");

        var bytes = new ConfigTransferService(ctx).Export();
        var names = EntryNames(bytes);

        Assert.Contains("skillset.md", names);
        Assert.Contains("provider-state.json", names);
        Assert.Contains("history/run1.json", names);
        Assert.Contains("manifest.json", names);
    }

    [Fact]
    public void Export_ExcludesModelAndTransientFiles()
    {
        var ctx = NewCtx(_tempRoot);
        Write(ctx.SkillsetPath, "# skills");
        Write(Path.Combine(ctx.RootDir, "models", "gemma.gguf"), "big-binary");
        Write(Path.Combine(ctx.RootDir, "all-listings.json.tmp"), "partial");
        Write(Path.Combine(ctx.RootDir, "portals.yml.bak"), "old");

        var names = EntryNames(new ConfigTransferService(ctx).Export());

        Assert.DoesNotContain(names, n => n.Contains("models/", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.DoesNotContain("portals.yml.bak", names);
    }

    [Fact]
    public void ExportThenImport_RestoresData_IntoDifferentRoot_AndBacksUp()
    {
        var sourceCtx = NewCtx(_tempRoot);
        Write(sourceCtx.SkillsetPath, "# my skills");
        Write(Path.Combine(sourceCtx.HistoryDir, "run1.json"), "{\"runId\":\"run1\"}");
        var bytes = new ConfigTransferService(sourceCtx).Export();

        // Fresh destination root with some pre-existing data that must be backed up.
        var destRoot = Path.Combine(_tempRoot, "dest");
        var destCtx = NewCtx(destRoot, email: "other@z");
        Write(destCtx.SkillsetPath, "OLD CONTENT");

        var result = new ConfigTransferService(destCtx).Import(new MemoryStream(bytes));

        Assert.True(result.Restored >= 2);
        Assert.Equal("# my skills", File.ReadAllText(destCtx.SkillsetPath));
        Assert.Equal("{\"runId\":\"run1\"}", File.ReadAllText(Path.Combine(destCtx.HistoryDir, "run1.json")));

        var backups = Directory.EnumerateDirectories(destCtx.RootDir, ".backup-*").ToList();
        Assert.Single(backups);
        Assert.Equal("OLD CONTENT", File.ReadAllText(Path.Combine(backups[0], "skillset.md")));
    }

    [Fact]
    public void Import_PreservesModelDirectory()
    {
        var sourceCtx = NewCtx(_tempRoot);
        Write(sourceCtx.SkillsetPath, "# skills");
        var bytes = new ConfigTransferService(sourceCtx).Export();

        var destRoot = Path.Combine(_tempRoot, "dest");
        var destCtx = NewCtx(destRoot, email: "other@z");
        var modelPath = Path.Combine(destCtx.RootDir, "models", "gemma.gguf");
        Write(modelPath, "big-binary");

        new ConfigTransferService(destCtx).Import(new MemoryStream(bytes));

        Assert.True(File.Exists(modelPath), "the LLM model must survive an import (not moved to backup)");
    }

    [Fact]
    public void Export_ExcludesHangfireQueue()
    {
        var ctx = NewCtx(_tempRoot);
        Write(ctx.SkillsetPath, "# skills");
        Write(Path.Combine(ctx.RootDir, "hangfire.db"), "sqlite");
        Write(Path.Combine(ctx.RootDir, "hangfire.db-wal"), "wal");
        Write(Path.Combine(ctx.RootDir, "hangfire.db-shm"), "shm");

        var names = EntryNames(new ConfigTransferService(ctx).Export());

        Assert.DoesNotContain(names, n => n.StartsWith("hangfire.db", StringComparison.Ordinal));
        Assert.Contains("skillset.md", names);
    }

    [Fact]
    public void Export_ExcludesRuntimeLogs()
    {
        var ctx = NewCtx(_tempRoot);
        Write(ctx.SkillsetPath, "# skills");
        Write(Path.Combine(ctx.RootDir, "logs", "host.log"), "WARN something");

        var names = EntryNames(new ConfigTransferService(ctx).Export());

        Assert.DoesNotContain(names, n => n.StartsWith("logs/", StringComparison.Ordinal));
        Assert.Contains("skillset.md", names);
    }

    [Fact]
    public void Import_LeavesLiveLogsDirectoryInPlace()
    {
        var sourceCtx = NewCtx(_tempRoot);
        Write(sourceCtx.SkillsetPath, "# my skills");
        var bytes = new ConfigTransferService(sourceCtx).Export();

        var destRoot = Path.Combine(_tempRoot, "dest");
        var destCtx = NewCtx(destRoot, email: "other@z");
        var log = Path.Combine(destCtx.RootDir, "logs", "host.log");
        Write(log, "live-log");

        new ConfigTransferService(destCtx).Import(new MemoryStream(bytes));

        Assert.True(File.Exists(log), "the live (locked) log dir must not be moved to backup during import");
        Assert.Equal("live-log", File.ReadAllText(log));
        var backups = Directory.EnumerateDirectories(destCtx.RootDir, ".backup-*").ToList();
        Assert.False(Directory.Exists(Path.Combine(backups[0], "logs")));
    }

    [Fact]
    public void Import_LeavesLiveHangfireQueueInPlace_AndRestoresJobHistory()
    {
        var sourceCtx = NewCtx(_tempRoot);
        Write(sourceCtx.SkillsetPath, "# my skills");
        Write(Path.Combine(sourceCtx.JobSearchDir, "job1.json"), "{\"id\":\"job1\"}");
        var bytes = new ConfigTransferService(sourceCtx).Export();

        var destRoot = Path.Combine(_tempRoot, "dest");
        var destCtx = NewCtx(destRoot, email: "other@z");
        var hangfire = Path.Combine(destCtx.RootDir, "hangfire.db");
        Write(hangfire, "live-queue");

        new ConfigTransferService(destCtx).Import(new MemoryStream(bytes));

        // The live (possibly locked) queue is left untouched — not moved to backup, so import can't throw on it.
        Assert.True(File.Exists(hangfire));
        Assert.Equal("live-queue", File.ReadAllText(hangfire));
        var backups = Directory.EnumerateDirectories(destCtx.RootDir, ".backup-*").ToList();
        Assert.Single(backups);
        Assert.False(File.Exists(Path.Combine(backups[0], "hangfire.db")));

        // Job history (jobsearch/*.json) rides along normally.
        Assert.Equal("{\"id\":\"job1\"}", File.ReadAllText(Path.Combine(destCtx.JobSearchDir, "job1.json")));
    }

    [Fact]
    public void Import_SkipsHangfireEntriesInArchive()
    {
        var ctx = NewCtx(_tempRoot);
        var live = Path.Combine(ctx.RootDir, "hangfire.db");
        Write(live, "live");
        var bytes = BuildZip(includeManifest: true, ("hangfire.db", "STALE"), ("skillset.md", "ok"));

        new ConfigTransferService(ctx).Import(new MemoryStream(bytes));

        Assert.Equal("live", File.ReadAllText(live)); // a transported queue never overwrites the live one
        Assert.Equal("ok", File.ReadAllText(ctx.SkillsetPath));
    }

    [Fact]
    public void Import_RejectsArchiveWithoutManifest()
    {
        var ctx = NewCtx(_tempRoot);
        var bytes = BuildZip(includeManifest: false, ("skillset.md", "hi"));

        Assert.Throws<InvalidRequestException>(() =>
            new ConfigTransferService(ctx).Import(new MemoryStream(bytes)));
    }

    [Fact]
    public void Import_RejectsNonZipFile()
    {
        var ctx = NewCtx(_tempRoot);
        var notAZip = new MemoryStream(Encoding.UTF8.GetBytes("this is not a zip"));

        Assert.Throws<InvalidRequestException>(() =>
            new ConfigTransferService(ctx).Import(notAZip));
    }

    [Fact]
    public void Import_RejectsNewerSchemaVersion()
    {
        var ctx = NewCtx(_tempRoot);
        var bytes = BuildZip(includeManifest: true, manifestSchemaVersion: 999, ("skillset.md", "hi"));

        Assert.Throws<InvalidRequestException>(() =>
            new ConfigTransferService(ctx).Import(new MemoryStream(bytes)));
    }

    [Fact]
    public void Import_SkipsZipSlipEntries()
    {
        var ctx = NewCtx(_tempRoot);
        var bytes = BuildZip(includeManifest: true, ("../evil.txt", "pwned"), ("skillset.md", "ok"));

        var result = new ConfigTransferService(ctx).Import(new MemoryStream(bytes));

        Assert.False(File.Exists(Path.Combine(_tempRoot, "evil.txt")));
        Assert.NotEmpty(result.Warnings);
    }

    private static string[] EntryNames(byte[] zipBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        return zip.Entries.Select(e => e.FullName).ToArray();
    }

    private static byte[] BuildZip(
        bool includeManifest,
        params (string Name, string Content)[] files)
        => BuildZip(includeManifest, ConfigTransferService.SchemaVersion, files);

    private static byte[] BuildZip(
        bool includeManifest,
        int manifestSchemaVersion,
        params (string Name, string Content)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry(name);
                using var s = entry.Open();
                s.Write(Encoding.UTF8.GetBytes(content));
            }

            if (includeManifest)
            {
                var manifest = new ConfigExportManifest(manifestSchemaVersion, "x@y", "0.0.0", DateTimeOffset.UnixEpoch);
                var entry = zip.CreateEntry("manifest.json");
                using var s = entry.Open();
                JsonSerializer.Serialize(s, manifest, JobmatchJsonOptions.Default);
            }
        }
        return ms.ToArray();
    }
}
