using Jobmatch.Features.Providers;

namespace Jobmatch.Tests.Features.Providers;

public sealed class SourceDetectionServiceTests
{
    private readonly SourceDetectionService _svc = new();

    private SourceCandidate DetectOne(string url)
    {
        var candidates = _svc.Detect(new Uri(url));
        Assert.Single(candidates);
        return candidates[0];
    }

    [Fact]
    public void Greenhouse_BoardUrl_ProducesApiDraft()
    {
        var c = DetectOne("https://boards.greenhouse.io/monzo");
        Assert.Equal("greenhouse", c.Kind);
        Assert.Equal(PortalType.Api, c.Draft.Type);
        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/monzo/jobs", c.Draft.Endpoint!.ToString());
        Assert.Equal("jobs", c.Draft.ResponseMapping!["items_path"]);
        Assert.Equal("absolute_url", c.Draft.ResponseMapping!["url"]);
    }

    [Fact]
    public void Ashby_BoardUrl_ProducesApiDraft()
    {
        var c = DetectOne("https://jobs.ashbyhq.com/pleo");
        Assert.Equal("ashby", c.Kind);
        Assert.Equal("https://api.ashbyhq.com/posting-api/job-board/pleo", c.Draft.Endpoint!.ToString());
        Assert.Equal("jobUrl", c.Draft.ResponseMapping!["url"]);
    }

    [Fact]
    public void Lever_BoardUrl_ProducesApiDraft()
    {
        var c = DetectOne("https://jobs.lever.co/h1");
        Assert.Equal("lever", c.Kind);
        Assert.Equal("https://api.lever.co/v0/postings/h1", c.Draft.Endpoint!.ToString());
        Assert.Equal("json", c.Draft.QueryParams!["mode"]);
    }

    [Fact]
    public void SmartRecruiters_BoardUrl_ProducesDkFilteredApiDraft()
    {
        var c = DetectOne("https://jobs.smartrecruiters.com/Netcompany1");
        Assert.Equal("smartrecruiters", c.Kind);
        Assert.Equal("https://api.smartrecruiters.com/v1/companies/Netcompany1/postings", c.Draft.Endpoint!.ToString());
        Assert.Equal("dk", c.Draft.QueryParams!["country"]);
        Assert.Equal("https://jobs.smartrecruiters.com/Netcompany1/{id}", c.Draft.ResponseMapping!["url_template"]);
        // Paginated by offset so a company with >100 DK roles isn't truncated at the first page.
        Assert.NotNull(c.Draft.Pagination);
        Assert.Equal("offset", c.Draft.Pagination!.Param);
        Assert.Equal(100, c.Draft.Pagination!.Step);
        Assert.Equal("limit", c.Draft.Pagination!.SizeParam);
    }

    [Fact]
    public void Teamtailor_SiteUrl_ProducesSitemapDraft()
    {
        var c = DetectOne("https://templafy.teamtailor.com/jobs");
        Assert.Equal("teamtailor", c.Kind);
        Assert.Equal(PortalType.TeamTailor, c.Draft.Type);
        Assert.Equal("https://templafy.teamtailor.com/sitemap.xml", c.Draft.Endpoint!.ToString());
    }

    [Fact]
    public void HrManager_ListUrl_ProducesHrManagerDraft()
    {
        var c = DetectOne("https://candidate.hr-manager.net/vacancies/list.aspx?customer=eg");
        Assert.Equal("hrmanager", c.Kind);
        Assert.Equal(PortalType.HrManager, c.Draft.Type);
        Assert.Contains("customer=eg", c.Draft.Endpoint!.ToString());
    }

    [Fact]
    public void RssUrl_ProducesRssDraft()
    {
        var c = DetectOne("https://www.jobindex.dk/jobsoegning.rss?q=c%23");
        Assert.Equal("rss", c.Kind);
        Assert.Equal(PortalType.Rss, c.Draft.Type);
        // Feeds that cap a page at ~20 items are paged via `page`; Size stays unset for a generic
        // feed, and the no-new-items guard makes it safe for feeds that ignore the cursor.
        Assert.NotNull(c.Draft.Pagination);
        Assert.Equal("page", c.Draft.Pagination!.Param);
        Assert.Null(c.Draft.Pagination!.Size);
    }

    [Fact]
    public void OracleRecruiting_BoardUrl_ProducesApiDraftMatchingTheCatalogPattern()
    {
        var c = DetectOne("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/jobs");

        Assert.Equal("oracle", c.Kind);
        Assert.Equal(PortalType.Api, c.Draft.Type);
        Assert.Equal(
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions",
            c.Draft.Endpoint!.ToString());
        // Without `expand` the requisition list comes back empty, and the rows sit one level down.
        Assert.Equal("requisitionList.secondaryLocations", c.Draft.QueryParams!["expand"]);
        Assert.Contains("siteNumber=CX_1001", (string)c.Draft.QueryParams!["finder"]!);
        Assert.Equal("items.0.requisitionList", c.Draft.ResponseMapping!["items_path"]);
        Assert.Equal(
            "https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/job/{Id}",
            c.Draft.ResponseMapping!["url_template"]);
        Assert.True(c.Draft.EnrichBody);
        // "ejqi" is a tenant id, not an employer — it must not be stamped on 140 jobs as the company.
        Assert.Equal("Oracle Recruiting Cloud (CX_1001)", c.DisplayName);
        Assert.Null(c.Draft.StaticFields);
    }

    [Fact]
    public void OracleRecruiting_JobDeepLink_ProducesTheSameBoardDraft()
    {
        var board = DetectOne("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/jobs");
        var deepLink = DetectOne("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/job/23989");

        Assert.Equal(board.Draft.Endpoint, deepLink.Draft.Endpoint);
        Assert.Equal(board.Draft.QueryParams!["finder"], deepLink.Draft.QueryParams!["finder"]);
    }

    [Fact]
    public void OracleRecruiting_RestUrl_TakesSiteNumberFromTheFinderParam()
    {
        var c = DetectOne(
            "https://fa-ewto-saasfaprod1.fa.ocs.oraclecloud.com/hcmRestApi/resources/latest/"
            + "recruitingCEJobRequisitions?finder=findReqs;siteNumber=CX_2,limit=50");

        Assert.Equal("oracle", c.Kind);
        Assert.Contains("siteNumber=CX_2", (string)c.Draft.QueryParams!["finder"]!);
    }

    [Fact]
    public void OracleRecruiting_LocalisedBoard_KeepsTheLanguageInJobLinks()
    {
        var c = DetectOne("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/da/sites/CX_1001/jobs");
        Assert.Contains("/CandidateExperience/da/sites/CX_1001/job/", c.Draft.ResponseMapping!["url_template"]);
    }

    [Fact]
    public void OracleHostWithoutASiteNumber_ProducesNoCandidate()
    {
        Assert.Empty(_svc.Detect(new Uri("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/")));
    }

    [Fact]
    public void WithBrand_RenamesCandidateAndItsStoredConfig()
    {
        var c = DetectOne("https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/jobs");

        var branded = SourceDetectionService.WithBrand(c, "Danske Bank");

        Assert.Equal("Danske Bank", branded.DisplayName);
        Assert.Equal("Danske Bank", branded.Draft.DisplayName);
        Assert.Equal("oracle-danske-bank", branded.Draft.Name);
        Assert.Equal("Danske Bank", branded.Draft.StaticFields!["company"]);
        // The endpoint is what the board actually is — renaming must not touch it.
        Assert.Equal(c.Draft.Endpoint, branded.Draft.Endpoint);
    }

    [Fact]
    public void UnknownUrl_ProducesNoCandidate()
    {
        Assert.Empty(_svc.Detect(new Uri("https://example.com/careers")));
    }

    [Fact]
    public void BuildManual_ProducesManualDraftWithImportHint()
    {
        var c = _svc.BuildManual("My Board");
        Assert.Equal("manual", c.Kind);
        Assert.Equal(PortalType.Manual, c.Draft.Type);
        Assert.Null(c.Draft.Endpoint);
        Assert.Contains("manual-my-board", c.Draft.Notes);
    }
}
