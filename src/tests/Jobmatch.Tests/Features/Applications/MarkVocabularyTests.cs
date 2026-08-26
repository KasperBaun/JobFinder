using System.Text.Json;
using Jobmatch.Features.Applications;
using Jobmatch.Infrastructure.Json;

namespace Jobmatch.Tests.Features.Applications;

// These strings are in every user's marks.json and in the GUI's TypeScript. The enums may gain
// members; an existing member's spelling is frozen. Both directions are pinned because the JSON
// converter and the hand-rolled marks codec share one map — this fails if either drifts.
public sealed class MarkVocabularyTests
{
    [Theory]
    [InlineData(MarkKind.Good, "good")]
    [InlineData(MarkKind.Bad, "bad")]
    public void MarkKindKeepsItsPersistedSpelling(MarkKind kind, string wire)
    {
        Assert.Equal(wire, kind.ToWire());
        Assert.Equal(kind, MarkKinds.TryParse(wire));
        Assert.Equal($"\"{wire}\"", JsonSerializer.Serialize(kind, JobmatchJsonOptions.Default));
        Assert.Equal(kind, JsonSerializer.Deserialize<MarkKind>($"\"{wire}\"", JobmatchJsonOptions.Default));
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied, "applied")]
    [InlineData(ApplicationStatus.Interview, "interview")]
    [InlineData(ApplicationStatus.Offer, "offer")]
    [InlineData(ApplicationStatus.Rejected, "rejected")]
    [InlineData(ApplicationStatus.NoResponse, "no-response")]
    public void ApplicationStatusKeepsItsPersistedSpelling(ApplicationStatus status, string wire)
    {
        Assert.Equal(wire, status.ToWire());
        Assert.Equal(status, ApplicationStatuses.TryParse(wire));
        Assert.Equal($"\"{wire}\"", JsonSerializer.Serialize(status, JobmatchJsonOptions.Default));
        Assert.Equal(status, JsonSerializer.Deserialize<ApplicationStatus>($"\"{wire}\"", JobmatchJsonOptions.Default));
    }

    [Fact]
    public void EveryMemberIsPinnedByATheoryAbove()
    {
        Assert.Equal(2, Enum.GetValues<MarkKind>().Length);
        Assert.Equal(5, Enum.GetValues<ApplicationStatus>().Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("maybe")]
    [InlineData("noResponse")]
    public void UnrecognisedInputParsesToNullRatherThanThrowing(string wire)
    {
        Assert.Null(MarkKinds.TryParse(wire));
        Assert.Null(ApplicationStatuses.TryParse(wire));
    }
}
