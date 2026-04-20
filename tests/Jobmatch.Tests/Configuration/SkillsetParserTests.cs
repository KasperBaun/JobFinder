using Jobmatch;
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class SkillsetParserTests
{
    private const string ValidSkillset = """
        ---
        name: Alex Example
        location: Remote, EU timezone
        experience_years: 5
        target_roles:
          - Software Engineer
          - Backend Developer
        remote_preference: remote
        seniority: mid
        languages:
          - English
        employment_types:
          - full-time
          - consulting
        ---

        ## Primary stack
        Must-have.

        - Python
        - TypeScript

        ## Secondary stack
        Nice-to-have.

        - Kubernetes

        ## Domains
        - Internal tools

        ## Disqualifiers
        - unpaid
        """;

    [Fact]
    public void Parse_Valid_Frontmatter_And_Sections()
    {
        var skillset = SkillsetParser.Parse(ValidSkillset);

        Assert.Equal("Alex Example", skillset.Name);
        Assert.Equal("Remote, EU timezone", skillset.Location);
        Assert.Equal(5, skillset.ExperienceYears);
        Assert.Equal(RemotePreference.Remote, skillset.RemotePreference);
        Assert.Equal(Seniority.Mid, skillset.Seniority);
        Assert.Equal(new[] { "Software Engineer", "Backend Developer" }, skillset.TargetRoles);
        Assert.Equal(new[] { "Python", "TypeScript" }, skillset.PrimaryStack);
        Assert.Equal(new[] { "Kubernetes" }, skillset.SecondaryStack);
        Assert.Equal(new[] { "Internal tools" }, skillset.Domains);
        Assert.Equal(new[] { "unpaid" }, skillset.Disqualifiers);
        Assert.Equal(new[] { "English" }, skillset.Languages);
        Assert.Equal(new[] { "full-time", "consulting" }, skillset.EmploymentTypes);
    }

    [Fact]
    public void Parse_Missing_Frontmatter_Throws()
    {
        var content = """
            ## Primary stack
            - Python
            """;
        var ex = Assert.Throws<ConfigException>(() => SkillsetParser.Parse(content));
        Assert.Contains("YAML frontmatter", ex.Message);
    }

    [Fact]
    public void Parse_Missing_Required_Field_Throws()
    {
        var content = """
            ---
            location: Copenhagen
            experience_years: 5
            remote_preference: remote
            seniority: mid
            ---

            ## Primary stack
            - C#
            """;
        var ex = Assert.Throws<ConfigException>(() => SkillsetParser.Parse(content));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void Parse_Invalid_Enum_Value_Throws()
    {
        var content = """
            ---
            name: A
            location: B
            experience_years: 1
            remote_preference: part-time
            seniority: mid
            ---

            ## Primary stack
            - X
            """;
        var ex = Assert.Throws<ConfigException>(() => SkillsetParser.Parse(content));
        Assert.Contains("remote_preference", ex.Message);
    }

    [Fact]
    public void Parse_Empty_PrimaryStack_Section_Returns_Empty_List()
    {
        var content = """
            ---
            name: A
            location: B
            experience_years: 1
            remote_preference: any
            seniority: any
            ---

            ## Primary stack

            ## Domains
            - foo
            """;
        var skillset = SkillsetParser.Parse(content);
        Assert.Empty(skillset.PrimaryStack);
        Assert.Equal(new[] { "foo" }, skillset.Domains);
    }

    [Fact]
    public void Parse_Unknown_Section_Is_Tolerated()
    {
        var content = """
            ---
            name: A
            location: B
            experience_years: 1
            remote_preference: any
            seniority: any
            ---

            ## Primary stack
            - X

            ## Notes
            - some note

            ## Domains
            - foo
            """;
        var skillset = SkillsetParser.Parse(content);
        Assert.Equal(new[] { "X" }, skillset.PrimaryStack);
        Assert.Equal(new[] { "foo" }, skillset.Domains);
    }

    [Fact]
    public void Parse_Asterisk_Bullets_Are_Accepted()
    {
        var content = """
            ---
            name: A
            location: B
            experience_years: 1
            remote_preference: any
            seniority: any
            ---

            ## Primary stack
            * X
            * Y
            """;
        var skillset = SkillsetParser.Parse(content);
        Assert.Equal(new[] { "X", "Y" }, skillset.PrimaryStack);
    }

    [Fact]
    public void Serialize_Roundtrip_Produces_Equivalent_Skillset()
    {
        var original = SkillsetParser.Parse(ValidSkillset);
        var serialized = SkillsetParser.Serialize(original);
        var reparsed = SkillsetParser.Parse(serialized);

        Assert.Equal(original.Name, reparsed.Name);
        Assert.Equal(original.Location, reparsed.Location);
        Assert.Equal(original.ExperienceYears, reparsed.ExperienceYears);
        Assert.Equal(original.RemotePreference, reparsed.RemotePreference);
        Assert.Equal(original.Seniority, reparsed.Seniority);
        Assert.Equal(original.PrimaryStack, reparsed.PrimaryStack);
        Assert.Equal(original.SecondaryStack, reparsed.SecondaryStack);
        Assert.Equal(original.Domains, reparsed.Domains);
        Assert.Equal(original.Disqualifiers, reparsed.Disqualifiers);
        Assert.Equal(original.TargetRoles, reparsed.TargetRoles);
        Assert.Equal(original.Languages, reparsed.Languages);
        Assert.Equal(original.EmploymentTypes, reparsed.EmploymentTypes);
    }

    [Fact]
    public void Parse_Handles_CRLF_Line_Endings()
    {
        var crlf = ValidSkillset.Replace("\n", "\r\n");
        var skillset = SkillsetParser.Parse(crlf);
        Assert.Equal("Alex Example", skillset.Name);
        Assert.Equal(new[] { "Python", "TypeScript" }, skillset.PrimaryStack);
    }
}
