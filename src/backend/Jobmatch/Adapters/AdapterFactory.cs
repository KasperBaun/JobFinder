using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public static class AdapterFactory
{
    public static IJobPortalAdapter? Create(PortalConfig portal, HttpClient http, ILogger logger, string importsDirectory)
    {
        return portal.Type switch
        {
            PortalType.Api => new ApiAdapter(portal, http, logger),
            PortalType.Rss => new RssAdapter(portal, http, logger),
            PortalType.Manual => new ManualAdapter(portal, http, logger, importsDirectory),
            PortalType.Html => new HtmlAdapter(portal, http, logger),
            _ => null,
        };
    }
}
