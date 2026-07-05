using Jobmatch.Models;
using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

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
