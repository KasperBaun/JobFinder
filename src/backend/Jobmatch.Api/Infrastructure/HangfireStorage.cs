using Jobmatch.Features.Identity;
using Jobmatch.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Infrastructure;

/// <summary>Where Hangfire's local job queue lives.</summary>
public static class HangfireStorage
{
    /// <summary>
    /// hangfire.db is transient job-queue infrastructure that Hangfire opens at server start —
    /// before first-run setup may have chosen a data directory. Use the configured directory when
    /// there is one, else the stable per-user fallback, so the job server starts without forcing
    /// the deferred UserContext (which throws SetupRequiredException until setup completes).
    /// </summary>
    public static string DbPath(IServiceProvider sp)
    {
        var provider = sp.GetRequiredService<IUserContextProvider>();
        var root = provider.IsConfigured ? provider.Current.RootDir : DataRoot.EnsureFallback();
        return Path.Combine(root, "hangfire.db");
    }
}
