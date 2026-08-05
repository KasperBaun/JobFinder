using Jobmatch.Adapters;
using Jobmatch.Models;

namespace Jobmatch.Tests.Adapters;

// Remote-mode inference: title and location are strong signals, the description only counts when it
// carries an unambiguous phrase, and a negated phrase is never evidence. Remote exempts a listing
// from the R-105 radius filter, so a false positive there hides a foreign office job.
public sealed class RemoteModeInferenceTests
{
    [Theory]
    [InlineData("SSC Support Manager-Remote", "Timisoara, Romania")]
    [InlineData("Sales Manager", "Poland; Remote")]
    [InlineData("Sr. Software Engineer - Search", "India Remote")]
    [InlineData("Embedded Electronic Engineer", "Copenhagen SV and remote work options")]
    public void Remote_In_Title_Or_Location_Is_Remote(string title, string location)
    {
        Assert.Equal(RemoteMode.Remote, BaseAdapter.InferRemoteMode(title, location, "We build payment infrastructure."));
    }

    [Theory]
    [InlineData("System Administrator", "Aarhus C, hybrid position")]
    [InlineData("Softwareudvikler til integrationer", "Kolding og mulighed for hjemmearbejde")]
    [InlineData("Senior Data Engineer", "Køge and the possibility to work from home")]
    public void Hybrid_In_Title_Or_Location_Is_Hybrid(string title, string location)
    {
        Assert.Equal(RemoteMode.Hybrid, BaseAdapter.InferRemoteMode(title, location, "Vi søger en udvikler."));
    }

    [Fact]
    public void Bare_Remote_Mention_In_Description_Is_Not_Remote()
    {
        var mode = BaseAdapter.InferRemoteMode(
            "Data Center Engineer",
            "Amsterdam",
            "Synchronize logistics with local DC-ops teams or remote hands.");

        Assert.Equal(RemoteMode.Unknown, mode);
    }

    [Theory]
    // The Adyen boilerplate behind 177 false positives in the measured corpus, including the
    // variant that wraps the negation in markup.
    [InlineData("We are an office-first company and value in-person collaboration; we do not offer remote-only roles.")]
    [InlineData("We value in-person collaboration; we <strong>do not</strong> offer remote-only roles.")]
    [InlineData("We are an office-first company and value in-person collaboration; we do not offer remote only roles")]
    [InlineData("We work in distributed teams, but might not be able to offer fully remote work or relocation.")]
    [InlineData("This is not a remote position; you are expected in the office.")]
    [InlineData("Our office is in central Copenhagen and we don't provide possibilities to work remotely.")]
    [InlineData("This is a full-time position in our San Francisco office (not remote).")]
    [InlineData("Fully remote work is not offered for this role.")]
    [InlineData("Employment type: Permanent Remote work: Not disclosed Weekly working time: Full-time")]
    [InlineData("Der er ikke mulighed for hjemmearbejde i denne stilling.")]
    [InlineData("Ansættelsestype: Fastansættelse Hjemmearbejde: Ikke oplyst Ugentlig arbejdstid: Fuldtid")]
    [InlineData("Stillingen er i Esbjerg. Hjemmearbejde: Tilbydes ikke.")]
    public void Negated_Remote_Phrase_Is_Not_Evidence(string description)
    {
        var mode = BaseAdapter.InferRemoteMode("Implementation Engineer", "Amsterdam", description);

        Assert.NotEqual(RemoteMode.Remote, mode);
        Assert.NotEqual(RemoteMode.Hybrid, mode);
    }

    [Theory]
    [InlineData("This is a fully remote role open to candidates across the EU.")]
    [InlineData("We are a remote-first company; you can work from anywhere in Europe.")]
    [InlineData("This is a remote role for candidates located in Montevideo, Uruguay.")]
    [InlineData("100% remote, with two on-site gatherings a year.")]
    [InlineData("Work Location: Remote")]
    [InlineData("Der er tale om fjernarbejde med enkelte besøg på kontoret.")]
    public void Unambiguous_Remote_Phrase_In_Description_Is_Remote(string description)
    {
        Assert.Equal(RemoteMode.Remote, BaseAdapter.InferRemoteMode("Software Engineer", "Copenhagen", description));
    }

    [Theory]
    [InlineData("Hybrid role with remote Fridays.")]
    [InlineData("SimCorp follows a global hybrid policy, asking employees to work from the office two days each week.")]
    [InlineData("You are comfortable working hybrid (coming to the office) several days per week.")]
    [InlineData("Fleksible rammer med mulighed for hjemmearbejde og en arbejdsplads, der prioriterer trivsel.")]
    [InlineData("Du kan arbejde hjemmefra et par dage om ugen.")]
    [InlineData("A great office and the opportunity to work from home 1-2 days a week.")]
    public void Unambiguous_Hybrid_Phrase_In_Description_Is_Hybrid(string description)
    {
        Assert.Equal(RemoteMode.Hybrid, BaseAdapter.InferRemoteMode("Software Engineer", "Copenhagen", description));
    }

    [Theory]
    // Word list markup, an i18n key and a job-board's own navigation chrome all carry the word
    // without saying anything about this job.
    [InlineData("<p data-attr=\"{&quot;469777815&quot;:&quot;hybridMultilevel&quot;}\">Responsibilities</p>")]
    [InlineData("{\"key\":\"General.City.Hybrid\",\"values\":[{\"culture\":\"en-US\",\"value\":\"Hybrid\"}]}")]
    [InlineData("Explore jobs Explore remote jobs Explore startups Explore content")]
    [InlineData("\"isRemote\":false,\"employmentType\":[],\"postingType\":\"1\"")]
    [InlineData("Bidrage til udviklingen af nye hybrid løsninger mellem ny og eksisterende mainframe.")]
    [InlineData("Multi-year experience in office, workplace or facilities management.")]
    public void Boilerplate_And_Markup_Are_Not_Evidence(string description)
    {
        Assert.Equal(RemoteMode.Unknown, BaseAdapter.InferRemoteMode("Software Engineer", "Copenhagen", description));
    }

    [Fact]
    public void Onsite_Phrase_Is_Onsite_When_Remote_Is_Denied()
    {
        var mode = BaseAdapter.InferRemoteMode(
            "Engineering Manager",
            "Aarhus",
            "We are based in Aarhus, and this is an on-site role in Aarhus with no remote option.");

        Assert.Equal(RemoteMode.Onsite, mode);
    }

    [Fact]
    public void Empty_Input_Is_Unknown()
    {
        Assert.Equal(RemoteMode.Unknown, BaseAdapter.InferRemoteMode(null, null, null));
    }
}
