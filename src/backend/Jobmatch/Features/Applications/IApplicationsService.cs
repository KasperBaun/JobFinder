namespace Jobmatch.Features.Applications;

public interface IApplicationsService
{
    IReadOnlyList<ApplicationEntry> List();
}
