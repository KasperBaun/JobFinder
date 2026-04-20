namespace Jobmatch.Models;

public sealed record HtmlSelectors(
    string ListSelector,
    string TitleSelector,
    string? LinkSelector = null,
    string? CompanySelector = null,
    string? LocationSelector = null,
    string? DescriptionSelector = null,
    string? UrlAttribute = "href");
