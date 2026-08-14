namespace Jobmatch.Api.Models;

/// <summary>The generic "it saved" acknowledgement shared by the providers, skillset and settings
/// write endpoints. Nothing to return beyond success, so every one of them answers with this.</summary>
public sealed record SaveResponse(bool Success, string? Error = null);
