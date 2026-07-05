using System.Net;
using Jobmatch.Models;
using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

// The "Test this source" button runs a connectivity fetch. Two properties matter for a good
// experience: it must be cheap (list-only, never a per-listing body-enrichment crawl of a third
// party) and its failures must read in plain language rather than raw .NET exception text.
public sealed class ProvidersServiceTestFetchTests
{
    [Fact]
    public void ForConnectivityTest_DisablesBodyEnrichment()
    {
        var portal = ApiPortal(enrichBody: true);

        var test = ProvidersService.ForConnectivityTest(portal);

        Assert.False(test.EnrichBody);
    }

    [Fact]
    public void ForConnectivityTest_PreservesEndpointAndMapping()
    {
        var portal = ApiPortal(enrichBody: true);

        var test = ProvidersService.ForConnectivityTest(portal);

        Assert.Equal(portal.Endpoint, test.Endpoint);
        Assert.Same(portal.ResponseMapping, test.ResponseMapping);
        Assert.Equal(portal.Name, test.Name);
    }

    [Fact]
    public void FriendlyError_NotFound_ExplainsBoardMovedOrClosed()
    {
        var ex = new HttpRequestException("Response status code does not indicate success: 404 (Not Found).", null, HttpStatusCode.NotFound);

        var msg = ProvidersService.FriendlyError(ex);

        Assert.Contains("404", msg);
        Assert.DoesNotContain("status code does not indicate success", msg);
    }

    [Fact]
    public void FriendlyError_Forbidden_MentionsAccessRefused()
    {
        var ex = new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden);

        Assert.Contains("access", ProvidersService.FriendlyError(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FriendlyError_ConnectionFailure_SaysCouldNotReach()
    {
        // A DNS/socket failure surfaces as HttpRequestException with no StatusCode.
        var ex = new HttpRequestException("No such host is known. (boards.example.invalid:443)");

        Assert.Contains("reach", ProvidersService.FriendlyError(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FriendlyError_Timeout_SaysTimedOut()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException wrapping a TimeoutException.
        var ex = new TaskCanceledException("A task was canceled.", new TimeoutException());

        Assert.Contains("timed out", ProvidersService.FriendlyError(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FriendlyError_UnrecognisedException_FallsBackToItsMessage()
    {
        var ex = new InvalidOperationException("portal 'x': response was not valid JSON");

        Assert.Equal(ex.Message, ProvidersService.FriendlyError(ex));
    }

    private static PortalConfig ApiPortal(bool enrichBody) => new(
        Name: "greenhouse-example",
        Type: PortalType.Api,
        Enabled: true,
        Endpoint: new Uri("https://boards-api.greenhouse.io/v1/boards/example/jobs"),
        ResponseMapping: new Dictionary<string, string> { ["items_path"] = "jobs", ["title"] = "title" },
        EnrichBody: enrichBody);
}
