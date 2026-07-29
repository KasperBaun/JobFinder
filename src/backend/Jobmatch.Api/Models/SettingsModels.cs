namespace Jobmatch.Api.Models;

public sealed record SetLanguageRequest(string? Language);

public sealed record LanguageResponse(string Language);
