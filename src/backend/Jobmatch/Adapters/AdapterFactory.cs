using Jobmatch.IO;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public static class AdapterFactory
{
    private delegate IJobPortalAdapter Factory(
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
        };

    public static IJobPortalAdapter? Create(
        PortalConfig portal, HttpClient http, ILogger logger,
        string importsDirectory, IFileSystem fs) =>
        Factories.TryGetValue(portal.Type, out var factory)
            ? factory(portal, http, logger, importsDirectory, fs)
            : null;
}
