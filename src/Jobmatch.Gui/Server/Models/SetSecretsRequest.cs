namespace Jobmatch.Gui.Server.Models;

public sealed record SetSecretsRequest(IReadOnlyDictionary<string, string> Values);
