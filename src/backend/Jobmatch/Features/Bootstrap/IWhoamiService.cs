namespace Jobmatch.Features.Bootstrap;

public sealed record WhoamiInfo(string Email, string DataDir, string ToolVersion);

public interface IWhoamiService
{
    WhoamiInfo Get();
}
