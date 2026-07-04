using Jobmatch.Configuration;

namespace Jobmatch;

/// <summary>What the setup screen needs: whether we're configured, and sensible pre-filled hints.</summary>
public sealed record SetupState(
    bool IsConfigured,
    bool ProfileExists,
    string? Email,
    string? DataDir,
    string SuggestedEmail,
    string SuggestedDataDir,
    string BootstrapPath);

/// <summary>
/// Owns the active <see cref="UserContext"/> and the first-run lifecycle. Resolution is deferred: if a
/// bootstrap config exists it configures immediately; otherwise the app runs in a "setup required"
/// state until the user confirms a data location via <see cref="Complete"/>. This is what lets the GUI
/// boot and show a setup screen on a machine with no git identity, instead of crashing.
/// </summary>
public interface IUserContextProvider
{
    bool IsConfigured { get; }

    /// <summary>The active context, or throws <see cref="SetupRequiredException"/> if setup is pending.</summary>
    UserContext Current { get; }

    SetupState State();

    /// <summary>Performs first-run setup: creates/seeds the chosen directory and persists the choice.</summary>
    UserContext Complete(string? email, string? dataDir);
}

public sealed class UserContextProvider : IUserContextProvider
{
    private readonly BootstrapStore _store;
    private readonly object _gate = new();
    private UserContext? _ctx;

    public UserContextProvider(BootstrapStore store)
    {
        _store = store;
        var config = store.TryLoad();
        if (config is not null)
        {
            _ctx = Build(config.Email, config.DataDir);
        }
    }

    public bool IsConfigured => _ctx is not null;

    public UserContext Current => _ctx ?? throw new SetupRequiredException(
        "First-run setup is required: choose where jobfinder should store your data.");

    public SetupState State()
    {
        var suggestedEmail = UserContext.TryResolveEmail() ?? string.Empty;
        return new SetupState(
            IsConfigured: _ctx is not null,
            ProfileExists: _ctx is not null && File.Exists(_ctx.SkillsetPath),
            Email: _ctx?.Email,
            DataDir: _ctx?.RootDir,
            SuggestedEmail: suggestedEmail,
            SuggestedDataDir: UserContext.SuggestDefaultDataDir(suggestedEmail),
            BootstrapPath: _store.Path);
    }

    public UserContext Complete(string? email, string? dataDir)
    {
        var trimmedEmail = email?.Trim();
        var trimmedDir = dataDir?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedEmail))
            throw new InvalidRequestException("An email (used to label your data) is required.");
        if (string.IsNullOrWhiteSpace(trimmedDir))
            throw new InvalidRequestException("A data folder is required.");

        lock (_gate)
        {
            var ctx = Build(trimmedEmail, trimmedDir);
            _store.Save(new BootstrapConfig(trimmedEmail, ctx.RootDir, DateTimeOffset.UtcNow));
            _ctx = ctx;
            return ctx;
        }
    }

    private static UserContext Build(string email, string dataDir)
    {
        // seedExamples: false — first run guides the user to create their own profile instead of
        // dropping in a generic example. The profile is written by the setup wizard (PUT /api/skillset).
        var ctx = UserContext.Resolve(emailOverride: email, dataDirOverride: dataDir, seedExamples: false);
        PortalsMigrationShim.RunIfNeeded(ctx.RootDir);
        return ctx;
    }
}
