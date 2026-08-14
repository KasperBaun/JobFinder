using Jobmatch.Models;
using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

/// <summary>
/// The pre-filter that decides which existing sources are worth a live fetch. Pure, so it runs
/// without touching the network; the fetch-and-compare half is exercised end-to-end by hand.
/// </summary>
public sealed class ProvidersServiceOverlapTests
{
    private static PortalConfig Portal(int id, string name, string display, string endpoint, string? company = null) =>
        new(
            Name: name,
            Type: PortalType.Api,
            Enabled: true,
            Endpoint: new Uri(endpoint),
            DisplayName: display,
            StaticFields: company is null ? null : new Dictionary<string, string> { ["company"] = company })
        { Id = id };

    private static readonly PortalConfig DanskeBank = Portal(
        44, "oracle-danskebank", "Danske Bank (Oracle)",
        "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions",
        "Danske Bank");

    private static readonly PortalConfig Milestone = Portal(
        47, "oracle-milestone", "Milestone Systems (Oracle)",
        "https://fa-ewto-saasfaprod1.fa.ocs.oraclecloud.com/hcmRestApi/resources/latest/recruitingCEJobRequisitions",
        "Milestone Systems");

    private static readonly PortalConfig Unrelated = Portal(
        3, "greenhouse-monzo", "Monzo", "https://boards-api.greenhouse.io/v1/boards/monzo/jobs", "Monzo");

    [Fact]
    public void CatalogEntrySharingTheDraftsName_IsStillProbed()
    {
        // Regression: link discovery names an Oracle board after the careers page it came from, which
        // lands on exactly the catalog entry's name. Treating that as "this is the draft itself" is
        // what made the check silently pass a source the user already had.
        var draft = Portal(0, "oracle-danskebank", "Danskebank",
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions");

        var probes = ProvidersService.RankProbes([DanskeBank, Milestone, Unrelated], draft, "ejqi.fa.ocs.oraclecloud.eu")
            .ToList();

        Assert.Equal(44, probes[0].Id);
    }

    [Fact]
    public void SameTenantHost_OutranksAnotherBoardOnTheSamePlatform()
    {
        var draft = Portal(0, "oracle-ejqi-cx-1001", "Ejqi",
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions");

        var probes = ProvidersService.RankProbes([Milestone, DanskeBank], draft, null).ToList();

        Assert.Equal(44, probes[0].Id);
    }

    [Fact]
    public void UnrelatedSources_AreNotWorthFetching()
    {
        var draft = Portal(0, "greenhouse-acme", "Acme", "https://boards-api.greenhouse.io/v1/boards/acme/jobs");

        Assert.Empty(ProvidersService.RankProbes([DanskeBank, Milestone], draft, null));
    }

    [Fact]
    public void ProbeListIsCapped_SoAddingASourceNeverFansOutAcrossTheCatalog()
    {
        var draft = Portal(0, "oracle-x", "Danske Bank",
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions");
        var catalog = Enumerable.Range(0, 20)
            .Select(i => Portal(i, $"oracle-danske-{i}", "Danske Bank",
                "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions"))
            .ToList();

        Assert.Equal(3, ProvidersService.RankProbes(catalog, draft, null).Count());
    }

    [Fact]
    public void DominantHost_IgnoresAStrayOffHostJobLink()
    {
        string[] urls =
        [
            "https://ejqi.fa.ocs.oraclecloud.eu/job/1",
            "https://ejqi.fa.ocs.oraclecloud.eu/job/2",
            "https://ejqi.fa.ocs.oraclecloud.eu/job/3",
            "https://linkedin.com/jobs/4",
        ];

        Assert.Equal("ejqi.fa.ocs.oraclecloud.eu", ProvidersService.DominantHost(urls));
    }

    [Fact]
    public void DominantHost_IsNullWhenJobsAreScatteredAcrossHosts()
    {
        string[] urls = ["https://a.dk/1", "https://b.dk/2", "https://c.dk/3", "https://d.dk/4"];

        Assert.Null(ProvidersService.DominantHost(urls));
    }
}
