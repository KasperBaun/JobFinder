namespace Jobmatch.Models;

public enum PortalType { Api, Rss, Html, Manual, TeamTailor }

public sealed record PortalConfig(
    string Name,
    PortalType Type,
    bool Enabled = true,
    Uri? Endpoint = null,
    IReadOnlyDictionary<string, object?>? QueryParams = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? ResponseMapping = null,
    HtmlSelectors? Html = null,
    double RateLimitRps = 1.0,
    string? Notes = null,
    IReadOnlyDictionary<string, string>? StaticFields = null,
    string? Method = null,
    IReadOnlyDictionary<string, object?>? BodyTemplate = null,
    PaginationConfig? Pagination = null,
    int Id = 0,
    string? RequiresSecret = null,
    string? DisplayName = null,
    bool EnrichBody = false);

public sealed record PaginationConfig(
    string Param,
    int Start = 1,
    int Step = 1,
    string? SizeParam = null,
    int? Size = null,
    int MaxPages = 5);
