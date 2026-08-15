using Jobmatch.Infrastructure.Paths;
using Jobmatch.Infrastructure;

namespace Jobmatch.Features.Identity;

public sealed class WhoamiService(UserContext ctx) : IWhoamiService
{
    public WhoamiInfo Get() => new(ctx.Email, ctx.RootDir, ToolVersion.Current);
}
