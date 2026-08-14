namespace Jobmatch.Features.Cv;

// Exactly one of Text / FileBytes / Url must be set.
public sealed record CvSource(string? Text, byte[]? FileBytes, string? FileName, string? Url);
