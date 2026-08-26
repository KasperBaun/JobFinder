using Jobmatch.Features.Cv;
using JobmatchUserContext = Jobmatch.Infrastructure.Paths.UserContext;

namespace Jobmatch.Tests.Features.Drafting;

public sealed class CvDocumentStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly JobmatchUserContext _ctx;

    public CvDocumentStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "cv-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _ctx = JobmatchUserContext.Resolve(
            emailOverride: "cv@example.com", repoRoot: _tempRoot, seedExamples: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Find_BeforeAnythingIsSaved_IsNull()
    {
        Assert.Null(new CvDocumentStore(_ctx).Find());
    }

    [Fact]
    public void Save_ThenFind_RoundTrips()
    {
        var store = new CvDocumentStore(_ctx);

        store.Save("Jane Doe\n\nSenior Developer at Acme");

        Assert.Contains("Senior Developer at Acme", store.Find());
    }

    [Fact]
    public void Save_NormalizesWhitespace()
    {
        var store = new CvDocumentStore(_ctx);

        store.Save("Jane   Doe\r\n\r\n\r\n\r\nDeveloper");

        var stored = store.Find()!;
        Assert.Contains("Jane Doe", stored);
        Assert.DoesNotContain("\r", stored);
        Assert.DoesNotContain("\n\n\n", stored);
    }

    [Fact]
    public void Save_Twice_ReplacesRatherThanAppends()
    {
        var store = new CvDocumentStore(_ctx);

        store.Save("first version");
        store.Save("second version");

        var stored = store.Find()!;
        Assert.DoesNotContain("first", stored);
        Assert.Contains("second", stored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Save_BlankText_IsRejected(string text)
    {
        Assert.Throws<InvalidRequestException>(() => new CvDocumentStore(_ctx).Save(text));
    }

    [Fact]
    public void Find_WhitespaceOnlyFile_IsTreatedAsAbsent()
    {
        File.WriteAllText(_ctx.CvPath, "   \n  ");

        Assert.Null(new CvDocumentStore(_ctx).Find());
    }

    [Fact]
    public void Save_WritesToCvPathUnderTheUserDirectory()
    {
        new CvDocumentStore(_ctx).Save("text");

        Assert.True(File.Exists(_ctx.CvPath));
        Assert.StartsWith(_ctx.RootDir, _ctx.CvPath, StringComparison.Ordinal);
    }
}
