namespace Jobmatch.Gui.Server.Models;

public sealed record MarkRequest(string RunId, string ListingId, string? Mark);

public sealed record MarkResponse(bool Success, string? Error = null);
