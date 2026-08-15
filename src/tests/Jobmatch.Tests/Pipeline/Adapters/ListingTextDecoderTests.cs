using Jobmatch.Search.Fetching;
using System.Text.Json;
using Jobmatch.Domain;
using Jobmatch.Search.Fetching.Adapters;

namespace Jobmatch.Tests.Pipeline.Adapters;

public sealed class ListingTextDecoderTests
{
    private static Listing Make(string title, string? company = null, string? location = null) => new(
        Id: "id-1",
        Portal: "portal",
        Title: title,
        Company: company,
        Location: location,
        RemoteMode: RemoteMode.Unknown,
        Description: "desc",
        Url: new Uri("https://example.com/job"),
        PostedAt: null,
        FetchedAt: DateTimeOffset.UnixEpoch,
        Raw: JsonDocument.Parse("{}").RootElement);

    [Fact]
    public void Decodes_entities_in_title_company_and_location()
    {
        var decoded = ListingTextDecoder.Decode(Make(
            "Fullstack Developer (.NET &amp; Angular)",
            "Work &amp; Security A/S",
            "K&#248;benhavn"));

        Assert.Equal("Fullstack Developer (.NET & Angular)", decoded.Title);
        Assert.Equal("Work & Security A/S", decoded.Company);
        Assert.Equal("København", decoded.Location);
    }

    [Fact]
    public void Returns_the_same_instance_when_nothing_needs_decoding()
    {
        var listing = Make("Senior .NET Developer", "Acme", "Aarhus");
        Assert.Same(listing, ListingTextDecoder.Decode(listing));
    }

    [Fact]
    public void Leaves_a_plain_ampersand_alone()
    {
        var decoded = ListingTextDecoder.Decode(Make("C# & Azure Engineer"));
        Assert.Equal("C# & Azure Engineer", decoded.Title);
    }
}
