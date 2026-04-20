using Jobmatch.Cli;

namespace Jobmatch.Tests;

public sealed class CliSmokeTests
{
    [Fact]
    public void CliApp_Create_Wires_Up_Without_Error()
    {
        var app = CliApp.Create();
        Assert.NotNull(app);
    }

    [Fact]
    public async Task CliApp_Help_Exits_Zero()
    {
        var app = CliApp.Create();
        var exitCode = await app.RunAsync(new[] { "--help" });
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("skillset")]
    [InlineData("listings")]
    [InlineData("verify")]
    public async Task CliApp_Subcommand_Stubs_Exit_Zero(string subcommand)
    {
        var app = CliApp.Create();
        var exitCode = await app.RunAsync(new[] { subcommand });
        Assert.Equal(0, exitCode);
    }
}
