using System.Net;
using Jobmatch.Domain;

namespace Jobmatch.Pipeline.Adapters;

/// <summary>
/// Decodes residual HTML entities in a listing's identifying fields ("Work &amp;amp; Security").
/// Individual adapters decode the strings they parse themselves, but API payloads can carry
/// pre-encoded text straight into Title/Company/Location — this is the single chokepoint every
/// fetched listing passes through, whatever the source.
/// </summary>
public static class ListingTextDecoder
{
    public static Listing Decode(Listing listing)
    {
        var title = DecodeField(listing.Title);
        var company = DecodeField(listing.Company);
        var location = DecodeField(listing.Location);
        if (ReferenceEquals(title, listing.Title)
            && ReferenceEquals(company, listing.Company)
            && ReferenceEquals(location, listing.Location))
        {
            return listing;
        }

        return listing with { Title = title!, Company = company, Location = location };
    }

    private static string? DecodeField(string? value)
    {
        if (value is null || !value.Contains('&')) return value;
        return WebUtility.HtmlDecode(value);
    }
}
