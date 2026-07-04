using Jobmatch;
using Jobmatch.Models;
using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

public sealed class SkillsetServiceCreateTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;

    public SkillsetServiceCreateTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "skillset-create-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private (UserContext ctx, SkillsetService svc) New()
    {
        var ctx = UserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);
        return (ctx, new SkillsetService(ctx));
    }

    private static SkillsetUpdate Essentials() => new(
        Name: "Jane Doe", Location: "Copenhagen", ExperienceYears: 5,
        TargetRoles: ["Backend Engineer"], RemotePreference: "remote", Seniority: "senior",
        PrimaryStack: ["C#", ".NET"], SecondaryStack: null, Domains: null, Disqualifiers: null,
        Languages: null, EmploymentTypes: null, Country: null, Region: null, Metro: null);

    [Fact]
    public void Update_CreatesFile_WhenMissing()
    {
        var (ctx, svc) = New();
        Assert.False(File.Exists(ctx.SkillsetPath));

        var saved = svc.Update(Essentials());

        Assert.True(File.Exists(ctx.SkillsetPath));
        Assert.Equal("Jane Doe", saved.Name);
        Assert.Equal("Copenhagen", saved.Location);
        Assert.Equal(RemotePreference.Remote, saved.RemotePreference);
        Assert.Equal(Seniority.Senior, saved.Seniority);
        Assert.Contains("C#", saved.PrimaryStack);
    }

    [Fact]
    public void Update_ThenReload_RoundTrips()
    {
        var (ctx, svc) = New();
        svc.Update(Essentials());

        var reloaded = svc.Get();
        Assert.Equal("Jane Doe", reloaded.Name);
        Assert.Equal(5, reloaded.ExperienceYears);
    }

    [Fact]
    public void Get_ThrowsFriendly_WhenNoProfile()
    {
        var (_, svc) = New();
        Assert.Throws<InvalidRequestException>(() => svc.Get());
    }
}
