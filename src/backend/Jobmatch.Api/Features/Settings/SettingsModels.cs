namespace Jobmatch.Api.Features.Settings;

public sealed record SetLanguageRequest(string? Language);

public sealed record LanguageResponse(string Language);
