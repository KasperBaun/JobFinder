namespace Jobmatch.Api.Features.Transfer;

public sealed record ImportResponse(int Restored, int Skipped, IReadOnlyList<string> Warnings);
