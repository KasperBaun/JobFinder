namespace Jobmatch.Api.Models;

public sealed record SetupStatusResponse(
    bool Configured,
    bool ProfileExists,
    string? Email,
    string? DataDir,
    string SuggestedEmail,
    string SuggestedDataDir,
    string BootstrapPath);

public sealed record SetupRequest(string? Email, string? DataDir);
