using Jobmatch.Models;

namespace Jobmatch.Adapters;

public interface IJobPortalAdapter
{
    string PortalName { get; }
    Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);

    // True after a fetch that stopped because it ran out of its page budget (MaxPages) while the last
    // page was still full and productive — i.e. the result is truncated and more likely exists. False
    // for a genuinely-exhausted fetch (empty/short/duplicate page) and for non-paginating sources.
    bool HitPageCap => false;
}
