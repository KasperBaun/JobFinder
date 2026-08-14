namespace Jobmatch.Platform.IO;

/// <summary>
/// Write-to-temp then replace, so a reader never observes a half-written file and a crash mid-write
/// leaves the previous contents intact. Every persisted file under <c>data/&lt;email&gt;/</c> goes
/// through here.
/// </summary>
public static class AtomicFile
{
    // A concurrent reader (SSE replay, history list) or an AV / file-sync scanner can briefly hold the
    // temp or target file, which surfaces on Windows as a transient IOException (sharing violation) or
    // UnauthorizedAccessException (access denied) from MoveFile. Retry rather than fail the write.
    private const int MoveAttempts = 10;
    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(20);

    public static void WriteAllText(string path, string contents)
        => Replace(path, temp => File.WriteAllText(temp, contents));

    /// <summary>For payloads written through a stream (serialisers that take one) rather than a string.</summary>
    public static void Write(string path, Action<Stream> writeContents)
        => Replace(path, temp =>
        {
            using var stream = File.Create(temp);
            writeContents(stream);
        });

    private static void Replace(string path, Action<string> writeTemp)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // The temp name is unique per write so two writers racing on the same target cannot
        // truncate each other's temp file before either has moved it into place.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        writeTemp(temp);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryDelay);
            }
        }
    }
}
