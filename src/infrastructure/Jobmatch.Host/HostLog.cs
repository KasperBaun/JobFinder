/// <summary>
/// Minimal terminal output formatting for the host process — startup banner, errors that
/// should surface to the user even though Kestrel logs are suppressed.
/// </summary>
public static class HostLog
{
    private static readonly object Lock = new();

    public static void Error(string message)
    {
        lock (Lock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  ✗ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }
}
