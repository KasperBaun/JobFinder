using Jobmatch.Platform.Paths;
using Jobmatch.Platform;

namespace Jobmatch.Features.Identity;

public sealed class WhoamiService(UserContext ctx) : IWhoamiService
{
    public WhoamiInfo Get() => new(ctx.Email, ctx.RootDir, ToolVersion.Current);
}
