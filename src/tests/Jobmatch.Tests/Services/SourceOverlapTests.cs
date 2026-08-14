using Jobmatch.Services;

namespace Jobmatch.Tests.Services;

public sealed class SourceOverlapTests
{
    private static string[] Jobs(int from, int count, string host = "jobs.example.com") =>
        [.. Enumerable.Range(from, count).Select(i => $"https://{host}/job/{i}")];

    [Fact]
    public void IdenticalJobSets_ReadAsADuplicate()
    {
        var m = SourceOverlap.Compare(44, "Danske Bank (Oracle)", Jobs(1, 40), Jobs(1, 40));

        Assert.NotNull(m);
        Assert.True(m.Duplicate);
        Assert.Equal(40, m.SharedCount);
        Assert.Equal(1.0, m.Ratio);
        Assert.Equal(44, m.ProviderId);
    }

    [Fact]
    public void CosmeticUrlDifferences_DoNotBreakTheMatch()
    {
        // Same board reached over http, with a trailing slash, a fragment and a shouted host.
        string[] existing =
        [
            "http://JOBS.example.com/job/1/",
            "https://jobs.example.com/job/2#apply",
            "https://jobs.example.com/job/3",
        ];

        var m = SourceOverlap.Compare(7, "Existing", Jobs(1, 3), existing);

        Assert.NotNull(m);
        Assert.True(m.Duplicate);
    }

    [Fact]
    public void QueryStringsStaySignificant()
    {
        // Plenty of boards carry the job id in the query, so two URLs differing only there are two jobs.
        string[] a = ["https://x.dk/job?id=1", "https://x.dk/job?id=2", "https://x.dk/job?id=3"];
        string[] b = ["https://x.dk/job?id=4", "https://x.dk/job?id=5", "https://x.dk/job?id=6"];

        Assert.Null(SourceOverlap.Compare(7, "Existing", a, b));
    }

    [Fact]
    public void PartialOverlap_IsReportedButNotAsADuplicate()
    {
        // 30 of the new source's 40 jobs also come from the existing one.
        var m = SourceOverlap.Compare(7, "Aggregator", Jobs(1, 40), Jobs(11, 40));

        Assert.NotNull(m);
        Assert.False(m.Duplicate);
        Assert.Equal(30, m.SharedCount);
    }

    [Fact]
    public void SmallBoardFullyInsideABigOne_StillReadsAsADuplicate()
    {
        // Ratio is measured against the smaller set, so the aggregator's other 200 jobs don't hide it.
        var m = SourceOverlap.Compare(7, "Aggregator", Jobs(1, 10), Jobs(1, 210));

        Assert.NotNull(m);
        Assert.True(m.Duplicate);
    }

    [Fact]
    public void UnrelatedSources_ProduceNoMatch()
    {
        Assert.Null(SourceOverlap.Compare(7, "Other", Jobs(1, 40), Jobs(500, 40)));
    }

    [Fact]
    public void TooFewJobsToCompare_ProducesNoMatch()
    {
        Assert.Null(SourceOverlap.Compare(7, "Tiny", Jobs(1, 2), Jobs(1, 2)));
    }

    [Theory]
    [InlineData("Danske Bank (Oracle)", "Danske Bank", true)]
    [InlineData("Danske Bank (Oracle)", "Danske Spil", false)]
    [InlineData("Milestone Systems (Oracle)", "Danske Bank", false)]
    // Platform words alone must not pair two unrelated boards that happen to share an ATS.
    [InlineData("Pleo (Ashby)", "Monzo (Ashby)", false)]
    public void NameSimilarity_SeparatesSameCompanyFromSamePlatform(string a, string b, bool similar)
    {
        Assert.Equal(similar, SourceOverlap.NameSimilarity(a, b) >= 0.5);
    }
}
