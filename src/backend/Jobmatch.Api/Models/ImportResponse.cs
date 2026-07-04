namespace Jobmatch.Api.Models;

public sealed record ImportResponse(int Restored, int Skipped, IReadOnlyList<string> Warnings);
