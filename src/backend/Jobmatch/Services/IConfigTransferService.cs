namespace Jobmatch.Services;

/// <summary>
/// Exports the active user's complete on-disk state (everything under <c>data/&lt;email&gt;/</c>) as a
/// single zip archive, and restores such an archive back into the active user's directory. The LLM
/// model and transient files are excluded from exports; imports back up the current state before
/// overwriting so a bad archive is always recoverable.
/// </summary>
public interface IConfigTransferService
{
    byte[] Export();
    ConfigImportResult Import(Stream archive);
}
