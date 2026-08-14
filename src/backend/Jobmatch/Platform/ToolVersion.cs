using System.Reflection;

namespace Jobmatch.Platform;

/// <summary>
/// The running build's version, as shown in the GUI and stamped into config exports. Read from the
/// entry assembly so the number matches the executable the user launched, not this library.
/// </summary>
public static class ToolVersion
{
    public static string Current =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "unknown";
}
