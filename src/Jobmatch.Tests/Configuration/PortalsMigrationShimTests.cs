using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

public sealed class PortalsMigrationShimTests
{
    [Fact]
    public void NoYaml_NoOp()
    {
        var dir = Directory.CreateTempSubdirectory("shim-noop");
        var stateBefore = ProviderStateLoader.LoadOrEmpty(Path.Combine(dir.FullName, "provider-state.json"));
        var migrated = PortalsMigrationShim.RunIfNeeded(dir.FullName);
        Assert.False(migrated);
        Assert.False(File.Exists(Path.Combine(dir.FullName, "provider-state.json")));
    }

    [Fact]
    public void DisabledFlagsCopiedIntoState()
    {
        var dir = Directory.CreateTempSubdirectory("shim-disabled");
        var yaml = """
            portals:
              - id: 1
                name: greenhouse-pleo
                type: api
                enabled: true
                endpoint: https://x/y
              - id: 2
                name: greenhouse-wolt
                type: api
                enabled: false
                endpoint: https://x/y
              - id: 3
                name: jobnet
                type: manual
                enabled: false
            """;
        File.WriteAllText(Path.Combine(dir.FullName, "portals.yml"), yaml);

        var migrated = PortalsMigrationShim.RunIfNeeded(dir.FullName);
        Assert.True(migrated);

        var state = ProviderStateLoader.LoadOrEmpty(Path.Combine(dir.FullName, "provider-state.json"));
        Assert.Contains(2, state.Disabled);
        Assert.Contains(3, state.Disabled);
        Assert.DoesNotContain(1, state.Disabled);

        Assert.True(File.Exists(Path.Combine(dir.FullName, "portals.yml.bak")));
        Assert.False(File.Exists(Path.Combine(dir.FullName, "portals.yml")));
    }

    [Fact]
    public void Idempotent_OnSecondRun()
    {
        var dir = Directory.CreateTempSubdirectory("shim-idempotent");
        File.WriteAllText(
            Path.Combine(dir.FullName, "portals.yml"),
            "portals:\n  - id: 1\n    name: a\n    type: manual\n    enabled: false\n");
        Assert.True(PortalsMigrationShim.RunIfNeeded(dir.FullName));
        Assert.False(PortalsMigrationShim.RunIfNeeded(dir.FullName)); // no yaml left
    }
}
