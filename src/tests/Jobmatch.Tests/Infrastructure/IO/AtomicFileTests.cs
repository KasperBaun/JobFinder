using Jobmatch.Infrastructure.IO;

namespace Jobmatch.Tests.Infrastructure.IO;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir;

    public AtomicFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "atomicfile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void WriteAllText_CreatesMissingDirectories()
    {
        var path = Path.Combine(_dir, "nested", "deeper", "state.json");

        AtomicFile.WriteAllText(path, "{}");

        Assert.Equal("{}", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_ReplacesExistingContent()
    {
        var path = Path.Combine(_dir, "state.json");
        AtomicFile.WriteAllText(path, "first");

        AtomicFile.WriteAllText(path, "second");

        Assert.Equal("second", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_LeavesNoTempFileBehind()
    {
        var path = Path.Combine(_dir, "state.json");

        AtomicFile.WriteAllText(path, "done");

        Assert.Empty(Directory.EnumerateFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void Write_StreamOverload_ProducesTheSameFile()
    {
        var path = Path.Combine(_dir, "stream.json");

        AtomicFile.Write(path, stream =>
        {
            using var writer = new StreamWriter(stream);
            writer.Write("streamed");
        });

        Assert.Equal("streamed", File.ReadAllText(path));
    }

    [Fact]
    public void ConcurrentWriters_AllSucceed_AndTheFileIsNeverPartial()
    {
        var path = Path.Combine(_dir, "contended.json");
        var payloads = Enumerable.Range(0, 20).Select(i => new string((char)('a' + i % 26), 500)).ToList();

        Parallel.ForEach(payloads, payload => AtomicFile.WriteAllText(path, payload));

        // Whichever writer landed last, the file is one complete payload — never a mix or a truncation.
        Assert.Contains(File.ReadAllText(path), payloads);
        Assert.Empty(Directory.EnumerateFiles(_dir, "*.tmp"));
    }
}
