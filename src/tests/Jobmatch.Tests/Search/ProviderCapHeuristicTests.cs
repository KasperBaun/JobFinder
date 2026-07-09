using Jobmatch.Models;
using Jobmatch.Search;

namespace Jobmatch.Tests.Search;

public sealed class ProviderCapHeuristicTests
{
    private static PortalConfig Portal(
        IReadOnlyDictionary<string, object?>? queryParams,
        PaginationConfig? pagination = null) => new(
        Name: "smartrecruiters",
        Type: PortalType.Api,
        Endpoint: new Uri("https://api.smartrecruiters.com/v1/companies/x/postings"),
        QueryParams: queryParams,
        Pagination: pagination);

    [Fact]
    public void LimitReached_True_WhenNonPaginatedCountEqualsNumericLimit()
    {
        var portal = Portal(new Dictionary<string, object?> { ["limit"] = "100" });
        Assert.True(ProviderCapHeuristic.LimitReached(portal, 100));
    }

    [Fact]
    public void LimitReached_False_WhenCountBelowLimit()
    {
        var portal = Portal(new Dictionary<string, object?> { ["limit"] = "100" });
        Assert.False(ProviderCapHeuristic.LimitReached(portal, 63));
    }

    [Fact]
    public void LimitReached_False_WhenPortalPaginates()
    {
        // A paginating source is covered by the real MaxPages cap detection, not this heuristic.
        var portal = Portal(
            new Dictionary<string, object?> { ["limit"] = "20" },
            new PaginationConfig(Param: "page", Size: 20, MaxPages: 5));
        Assert.False(ProviderCapHeuristic.LimitReached(portal, 20));
    }

    [Fact]
    public void LimitReached_False_WhenNoLimitParam()
    {
        var portal = Portal(new Dictionary<string, object?> { ["q"] = "developer" });
        Assert.False(ProviderCapHeuristic.LimitReached(portal, 50));
    }

    [Fact]
    public void LimitReached_False_WhenZeroResults()
    {
        var portal = Portal(new Dictionary<string, object?> { ["limit"] = "0" });
        Assert.False(ProviderCapHeuristic.LimitReached(portal, 0));
    }
}
