using System.Reflection;
using Jobmatch.Platform.Paths;

namespace Jobmatch.Features.Identity;

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
