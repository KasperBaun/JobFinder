using Jobmatch.Models;

namespace Jobmatch.Adapters;

public interface IJobPortalAdapter
{
    string PortalName { get; }
    Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);
}
