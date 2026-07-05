namespace Jobmatch.Configuration;

/// <summary>
/// Resolves the directory the host's log file lives in. Logs must land in the <em>same</em> data
/// directory the app actually reads and writes (the one recorded in <see cref="BootstrapConfig"/>),
/// not a divergent git-identity-resolved location. Before first-run setup there is no configured
/// directory yet, so a caller-supplied fallback is used instead.
/// </summary>
public static class LogLocation
{
    public static string ResolveRootDir(BootstrapStore store, Func<string> fallback)
    {
        var bootstrap = store.TryLoad();
        return bootstrap is not null ? bootstrap.DataDir : fallback();
    }
}
