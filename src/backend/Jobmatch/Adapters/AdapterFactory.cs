using Jobmatch.IO;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public static class AdapterFactory
{
    private delegate BaseAdapter Factory(
        PortalConfig portal, HttpClient http, ILogger logger,
        string importsDirectory, IFileSystem fs);

    private static readonly IReadOnlyDictionary<PortalType, Factory> Factories =
        new Dictionary<PortalType, Factory>
        {
            [PortalType.Api]        = (p, h, l, _, _)   => new ApiAdapter(p, h, l),
            [PortalType.Rss]        = (p, h, l, _, _)   => new RssAdapter(p, h, l),
            [PortalType.Html]       = (p, h, l, _, _)   => new HtmlAdapter(p, h, l),
            [PortalType.Manual]     = (p, h, l, dir, fs) => new ManualAdapter(p, h, l, dir, fs),
            [PortalType.TeamTailor] = (p, h, l, _, _)   => new TeamTailorAdapter(p, h, l),
            [PortalType.HrManager]  = (p, h, l, _, _)   => new HrManagerAdapter(p, h, l),
        };

    public static IJobPortalAdapter? Create(
        PortalConfig portal, HttpClient http, ILogger logger,
        string importsDirectory, IFileSystem fs, BodyFetchSession? fetchSession = null)
    {
        if (!Factories.TryGetValue(portal.Type, out var factory)) return null;
        var adapter = factory(portal, http, logger, importsDirectory, fs);
        adapter.FetchSession = fetchSession;
        return adapter;
    }
}
