using Jobmatch.Cli;
using Jobmatch.Tests.Integration;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Jobmatch.Tests;

[Collection("ProcessGlobalState")]
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

    [Fact]
    public async Task CliApp_Help_Lists_All_Three_Subcommands()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Ansi = false;

        await CliApp.Create(console).RunAsync(new[] { "--help" });

        var output = console.Output;
        Assert.Contains("skillset", output);
        Assert.Contains("listings", output);
        Assert.Contains("verify", output);
    }
}
