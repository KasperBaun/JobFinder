using Jobmatch.Verification;

namespace Jobmatch.Tests.Verification;

public sealed class ConfigVerifierTests : IDisposable
{
    private readonly string _root;

    public ConfigVerifierTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "jobmatch-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_root, "config"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void WriteFile(string relative, string content) =>
        File.WriteAllText(Path.Combine(_root, relative), content);

    private const string ValidSkillset = """
        ---
        name: A
        location: B
        experience_years: 5
        remote_preference: remote
        seniority: mid
        ---

        ## Primary stack
        - X
        """;

    private const string ValidPortals = """
        portals:
          - name: jobnet
            type: api
            enabled: true
            endpoint: https://job.jobnet.dk/CV/FindWork/Search
        """;

    private const string ValidRanking = """
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
        """;

    private void WriteValidSet()
    {
        WriteFile("config/skillset.md", ValidSkillset);
        WriteFile("config/portals.yml", ValidPortals);
        WriteFile("config/ranking.yml", ValidRanking);
    }

    [Fact]
    public async Task Verify_AllValid_Returns_No_Failures()
    {
        WriteValidSet();
        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();
        Assert.False(report.HasFailures, string.Join("; ", report.Checks.Where(c => c.Status == VerificationStatus.Fail).Select(c => c.Details)));
    }

    [Fact]
    public async Task Verify_Missing_Skillset_Returns_Fail()
    {
        WriteFile("config/portals.yml", ValidPortals);
        WriteFile("config/ranking.yml", ValidRanking);
        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();

        Assert.True(report.HasFailures);
        Assert.Contains(report.Checks, c => c.Name.StartsWith("Required config files") && c.Status == VerificationStatus.Fail);
    }

    [Fact]
    public async Task Verify_Weights_Not_Summing_To_One_Returns_Fail()
    {
        WriteFile("config/skillset.md", ValidSkillset);
        WriteFile("config/portals.yml", ValidPortals);
        WriteFile("config/ranking.yml", """
            weights:
              primary_stack: 0.50
              secondary_stack: 0.50
              seniority: 0.50
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            top_n: 10
            """);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();
        var rankingCheck = report.Checks.Single(c => c.Name == "Ranking weights valid");
        Assert.Equal(VerificationStatus.Fail, rankingCheck.Status);
        Assert.Contains("sum", rankingCheck.Details);
    }

    [Fact]
    public async Task Verify_Malformed_Portals_Returns_Fail_Not_Throw()
    {
        WriteFile("config/skillset.md", ValidSkillset);
        WriteFile("config/portals.yml", "this is: [not valid yaml");
        WriteFile("config/ranking.yml", ValidRanking);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();
        Assert.Contains(report.Checks, c => c.Name == "Portal config parses" && c.Status == VerificationStatus.Fail);
    }

    [Fact]
    public async Task Verify_Empty_PrimaryStack_Returns_Fail()
    {
        WriteFile("config/skillset.md", """
            ---
            name: A
            location: B
            experience_years: 5
            remote_preference: remote
            seniority: mid
            ---

            ## Primary stack
            """);
        WriteFile("config/portals.yml", ValidPortals);
        WriteFile("config/ranking.yml", ValidRanking);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();
        var skillsetCheck = report.Checks.Single(c => c.Name == "Skillset parses");
        Assert.Equal(VerificationStatus.Fail, skillsetCheck.Status);
        Assert.Contains("primary stack", skillsetCheck.Details);
    }

    [Fact]
    public async Task Verify_Manual_Portal_Without_Import_Returns_Warn()
    {
        WriteFile("config/skillset.md", ValidSkillset);
        WriteFile("config/portals.yml", """
            portals:
              - name: my-manual
                type: manual
                enabled: true
            """);
        WriteFile("config/ranking.yml", ValidRanking);

        var verifier = new ConfigVerifier(_root);
        var report = await verifier.VerifyAsync();
        var check = report.Checks.Single(c => c.Name == "Manual import files present");
        Assert.Equal(VerificationStatus.Warn, check.Status);
    }
}
