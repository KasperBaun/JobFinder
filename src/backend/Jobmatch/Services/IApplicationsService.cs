namespace Jobmatch.Services;

public interface IApplicationsService
{
    IReadOnlyList<ApplicationEntry> List();
}
