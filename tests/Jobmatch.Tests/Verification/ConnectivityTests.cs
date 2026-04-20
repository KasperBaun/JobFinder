using System.Net;
using Jobmatch.Verification;

namespace Jobmatch.Tests.Verification;

public sealed class ConnectivityTests : IDisposable
{
    private readonly string _root;

    public ConnectivityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "jobmatch-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "skillset.md"), """
            ---
            name: A
            location: B
            experience_years: 5
            remote_preference: remote
            seniority: mid
            ---

            ## Primary stack
            - X
            """);
        File.WriteAllText(Path.Combine(_root, "config", "ranking.yml"), """
            weights:
              primary_stack: 0.40
              secondary_stack: 0.15
              seniority: 0.15
              location_remote: 0.15
              domain: 0.10
              freshness: 0.05
            top_n: 10
            disqualifier_penalty: 0.0
            freshness_half_life_days: 14
            min_score_to_include: 0.25
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Connectivity_Disabled_Emits_No_Connectivity_Checks()
    {
        File.WriteAllText(Path.Combine(_root, "config", "portals.yml"), """
            portals:
              - name: jobnet
                type: api
                enabled: true
                endpoint: https://example.com/search
            """);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync(includeConnectivity: false);
        Assert.DoesNotContain(report.Checks, c => c.Name.StartsWith("Connectivity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Connectivity_Success_Is_Pass()
    {
        File.WriteAllText(Path.Combine(_root, "config", "portals.yml"), """
            portals:
              - name: jobnet
                type: api
                enabled: true
                endpoint: https://example.com/search
            """);

        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK));
        var verifier = new ConfigVerifier(_root, http);
        var report = await verifier.VerifyAsync(includeConnectivity: true);

        var check = report.Checks.Single(c => c.Name == "Connectivity: jobnet");
        Assert.Equal(VerificationStatus.Pass, check.Status);
    }

    [Fact]
    public async Task Connectivity_500_Is_Warn()
    {
        File.WriteAllText(Path.Combine(_root, "config", "portals.yml"), """
            portals:
              - name: jobnet
                type: api
                enabled: true
                endpoint: https://example.com/search
            """);

        using var http = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError));
        var verifier = new ConfigVerifier(_root, http);
        var report = await verifier.VerifyAsync(includeConnectivity: true);

        var check = report.Checks.Single(c => c.Name == "Connectivity: jobnet");
        Assert.Equal(VerificationStatus.Warn, check.Status);
    }

    [Fact]
    public async Task Connectivity_No_API_Portals_Is_Pass()
    {
        File.WriteAllText(Path.Combine(_root, "config", "portals.yml"), """
            portals:
              - name: mine
                type: manual
                enabled: true
            """);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync(includeConnectivity: true);

        var check = report.Checks.Single(c => c.Name == "Connectivity");
        Assert.Equal(VerificationStatus.Pass, check.Status);
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("ok") });
    }
}
