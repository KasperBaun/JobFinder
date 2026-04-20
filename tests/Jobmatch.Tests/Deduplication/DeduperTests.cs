using System.Text.Json;
using Jobmatch.Deduplication;
using Jobmatch.Models;

namespace Jobmatch.Tests.Deduplication;

public sealed class DeduperTests
{
    private static Listing Make(string portal, string title, string? company, string url)
    {
        return new Listing(
            Id: $"{portal}-{title}-{url}",
            Portal: portal,
            Title: title,
            Company: company,
            Location: null,
            RemoteMode: RemoteMode.Unknown,
            Description: string.Empty,
            Url: new Uri(url),
            PostedAt: null,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: JsonDocument.Parse("{}").RootElement.Clone());
    }

    [Fact]
    public void Deduplicate_IdenticalUrl_Collapses_To_One()
    {
        var input = new[]
        {
            Make("a", "Software Engineer", "Acme", "https://acme.com/jobs/1"),
            Make("b", "Software Engineer", "Acme", "https://acme.com/jobs/1"),
        };
        var result = Deduper.Deduplicate(input);
        Assert.Single(result);
    }

    [Fact]
    public void Deduplicate_Url_Differs_By_Fragment_Only_Are_Dupes()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/jobs/1"),
            Make("a", "Job A (variant)", "Acme", "https://acme.com/jobs/1#apply"),
        };
        Assert.Single(Deduper.Deduplicate(input));
    }

    [Fact]
    public void Deduplicate_Url_Differs_By_Trailing_Slash_Are_Dupes()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/jobs/1"),
            Make("b", "Job A", "Acme", "https://acme.com/jobs/1/"),
        };
        Assert.Single(Deduper.Deduplicate(input));
    }

    [Fact]
    public void Deduplicate_Same_Title_And_Company_Different_Urls_Are_Dupes()
    {
        var input = new[]
        {
            Make("linkedin", "Senior Engineer", "Acme Corp", "https://linkedin.com/jobs/1"),
            Make("jobnet", "Senior Engineer", "Acme Corp", "https://jobnet.dk/jobs/abc"),
        };
        Assert.Single(Deduper.Deduplicate(input));
    }

    [Fact]
    public void Deduplicate_Case_And_Whitespace_Differences_Still_Match()
    {
        var input = new[]
        {
            Make("a", "Senior   Engineer", "Acme  Corp", "https://a.com/1"),
            Make("b", "senior engineer", "ACME CORP", "https://b.com/2"),
        };
        Assert.Single(Deduper.Deduplicate(input));
    }

    [Fact]
    public void Deduplicate_Different_Companies_Are_Distinct()
    {
        var input = new[]
        {
            Make("a", "Senior Engineer", "Acme", "https://a.com/1"),
            Make("b", "Senior Engineer", "Globex", "https://b.com/1"),
        };
        Assert.Equal(2, Deduper.Deduplicate(input).Count);
    }

    [Fact]
    public void Deduplicate_Preserves_Order_Keeping_First_Occurrence()
    {
        var input = new[]
        {
            Make("a", "Job A", "Acme", "https://acme.com/1"),
            Make("a", "Job B", "Acme", "https://acme.com/2"),
            Make("b", "Job A", "Acme", "https://other.com/1"),
        };
        var result = Deduper.Deduplicate(input);
        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0].Portal);
        Assert.Equal("Job A", result[0].Title);
        Assert.Equal("Job B", result[1].Title);
    }
}
