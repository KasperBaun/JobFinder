using Jobmatch.Models;

namespace Jobmatch.Configuration;

public static class PortalsMigrationShim
{
    /// <summary>
    /// One-shot migration: if <c>{userDataDir}/portals.yml</c> exists, parse it,
    /// translate <c>enabled: false</c> entries into <c>provider-state.json.disabled[]</c>,
    /// rename the yaml to <c>portals.yml.bak</c>, and return true.
    /// Returns false if nothing to do. Safe to call on every startup.
    /// </summary>
    public static bool RunIfNeeded(string userDataDir)
    {
        var yamlPath = Path.Combine(userDataDir, "portals.yml");
        if (!File.Exists(yamlPath)) return false;

        IReadOnlyList<PortalConfig> portals;
        try { portals = PortalConfigLoader.Load(yamlPath); }
        catch { return false; }  // unreadable yaml — leave it; user can sort out manually

        var statePath = Path.Combine(userDataDir, "provider-state.json");
        var existing = ProviderStateLoader.LoadOrEmpty(statePath);
        var disabledIds = new HashSet<int>(existing.Disabled);
        foreach (var p in portals)
        {
            if (!p.Enabled && p.Id > 0) disabledIds.Add(p.Id);
        }
        var newState = new ProviderState(
            Disabled: disabledIds.OrderBy(i => i).ToArray(),
            Enabled: existing.Enabled,
            Secrets: existing.Secrets);
        ProviderStateLoader.Save(statePath, newState);

        var backupPath = Path.Combine(userDataDir, "portals.yml.bak");
        if (File.Exists(backupPath)) File.Delete(backupPath);
        File.Move(yamlPath, backupPath);
        Console.WriteLine($"[migration] portals.yml → provider-state.json ({disabledIds.Count} disabled); backup at portals.yml.bak");
        return true;
    }
}
