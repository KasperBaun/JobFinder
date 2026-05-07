namespace Jobmatch.IO;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern) =>
        Directory.EnumerateFiles(path, searchPattern);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
}
