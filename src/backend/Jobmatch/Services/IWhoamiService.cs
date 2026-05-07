using System.Reflection;

namespace Jobmatch.Services;

public sealed record WhoamiInfo(string Email, string DataDir, string ToolVersion);

public interface IWhoamiService
{
    WhoamiInfo Get();
}

public sealed class WhoamiService(UserContext ctx) : IWhoamiService
{
    public WhoamiInfo Get() => new(ctx.Email, ctx.RootDir, ResolveToolVersion());

    private static string ResolveToolVersion()
    {
        var entry = Assembly.GetEntryAssembly();
        return entry?.GetName().Version?.ToString(3)
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "unknown";
    }
}
