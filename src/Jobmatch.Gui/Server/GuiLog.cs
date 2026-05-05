namespace Jobmatch.Gui.Server;

/// <summary>
/// Formatted console output for GUI mode so the user can see what's happening in the terminal.
/// </summary>
public static class GuiLog
{
    private static readonly object Lock = new();

    public static void Step(string description, string status)
    {
        var symbol = status switch
        {
            "running" => "…",
            "done" => "✓",
            "failed" => "✗",
            _ => " ",
        };

        lock (Lock)
        {
            Console.ForegroundColor = status switch
            {
                "done" => ConsoleColor.Green,
                "failed" => ConsoleColor.Red,
                "running" => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray,
            };
            Console.Write($"  {symbol} ");
            Console.ResetColor();
            Console.WriteLine(description);
        }
    }

    public static void Info(string message)
    {
        lock (Lock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    {message}");
            Console.ResetColor();
        }
    }

    public static void Warning(string message)
    {
        lock (Lock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("  ⚠ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }

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

    public static void Action(string label)
    {
        lock (Lock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  → ");
            Console.ResetColor();
            Console.WriteLine(label);
        }
    }
}
