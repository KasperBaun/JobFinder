namespace Jobmatch.Models;

public enum PortalType { Api, Rss, Html, Manual }

public sealed record PortalConfig(
    string Name,
    PortalType Type,
    bool Enabled = true,
    Uri? BaseUrl = null,
    Uri? Endpoint = null,
    IReadOnlyDictionary<string, object?>? QueryParams = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? ResponseMapping = null,
    HtmlSelectors? Html = null,
    double RateLimitRps = 1.0,
    string? Notes = null,
    IReadOnlyDictionary<string, string>? StaticFields = null);
