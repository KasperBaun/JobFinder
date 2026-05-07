namespace Jobmatch.IO;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    Stream OpenRead(string path);
    string ReadAllText(string path);
}
