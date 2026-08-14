using Jobmatch.Domain;
using Jobmatch.Features.Skillsets;
using Jobmatch;

namespace Jobmatch.Tests.Configuration;

/// <summary>Frontmatter parsing/serialization of the radius-filter fields (R-105).</summary>
public sealed class SkillsetParserRadiusTests
{
    private static Skillset Minimal() => new(
        Name: "Jane", Location: "Copenhagen, Denmark", ExperienceYears: 5, TargetRoles: ["Engineer"],
        RemotePreference: RemotePreference.Any, Seniority: Seniority.Mid,
        PrimaryStack: ["C#"], SecondaryStack: [], Domains: [], Disqualifiers: [],
        Languages: ["en"], EmploymentTypes: ["full-time"]);

    [Fact]
    public void RoundTrip_Preserves_Address_Radius_And_Coordinates()
    {
        var skillset = Minimal() with
        {
            Address = "Rådhuspladsen 1, 1550 København V",
            RadiusKm = 42.5,
            Latitude = 55.67594,
            Longitude = 12.56553,
            ResolvedAddress = "Rådhuspladsen 1, 1550 København V",
        };

        var parsed = SkillsetParser.Parse(SkillsetParser.Serialize(skillset));

        Assert.Equal(skillset.Address, parsed.Address);
        Assert.Equal(42.5, parsed.RadiusKm);
        Assert.Equal(55.67594, parsed.Latitude);
        Assert.Equal(12.56553, parsed.Longitude);
        Assert.Equal(skillset.ResolvedAddress, parsed.ResolvedAddress);
    }

    [Fact]
    public void Absent_Keys_Parse_As_Nulls()
    {
        var parsed = SkillsetParser.Parse(SkillsetParser.Serialize(Minimal()));

        Assert.Null(parsed.Address);
        Assert.Null(parsed.RadiusKm);
        Assert.Null(parsed.Latitude);
        Assert.Null(parsed.Longitude);
        Assert.Null(parsed.ResolvedAddress);
    }

    [Fact]
    public void Serialize_Omits_Absent_Radius_Keys()
    {
        var text = SkillsetParser.Serialize(Minimal());

        Assert.DoesNotContain("address:", text);
        Assert.DoesNotContain("radius_km:", text);
        Assert.DoesNotContain("latitude:", text);
        Assert.DoesNotContain("longitude:", text);
        Assert.DoesNotContain("resolved_address:", text);
    }

    [Fact]
    public void Doubles_Parse_With_InvariantCulture()
    {
        var parsed = SkillsetParser.Parse("""
            ---
            name: Jane
            location: Copenhagen
            experience_years: 5
            target_roles: [Engineer]
            remote_preference: any
            seniority: mid
            radius_km: "12.5"
            latitude: "55.5"
            longitude: "12.25"
            ---

            ## Primary stack
            """);

        Assert.Equal(12.5, parsed.RadiusKm);
        Assert.Equal(55.5, parsed.Latitude);
        Assert.Equal(12.25, parsed.Longitude);
    }

    [Fact]
    public void NonNumeric_Radius_Throws_ConfigException()
    {
        var ex = Assert.Throws<ConfigException>(() => SkillsetParser.Parse("""
            ---
            name: Jane
            location: Copenhagen
            experience_years: 5
            target_roles: [Engineer]
            remote_preference: any
            seniority: mid
            radius_km: near
            ---

            ## Primary stack
            """));
        Assert.Contains("radius_km", ex.Message);
    }
}
