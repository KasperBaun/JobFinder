namespace Jobmatch.Services;

public sealed record ConfigExportManifest(
    int SchemaVersion,
    string Email,
    string ToolVersion,
    DateTimeOffset ExportedAt);

public sealed record ConfigImportResult(
    int Restored,
    int Skipped,
    IReadOnlyList<string> Warnings);
