using Jobmatch.Features.History;
using Jobmatch.Features.Providers;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Tests;

/// <summary>
/// The collaborators that several services now share, built straight from a test's
/// <see cref="UserContext"/>. Both are thin readers over the user's directory, so tests use the real
/// implementations rather than fakes — staging a file is what a test wants to assert on anyway.
/// </summary>
internal static class TestServices
{
    public static IProviderCatalog Catalog(UserContext ctx) => new ProviderCatalog(ctx);

    public static IRunHistoryStore Runs(UserContext ctx) => new RunHistoryStore(ctx);
}
