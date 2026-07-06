namespace Jobmatch;

public abstract class JobfinderException(string message) : Exception(message);

public sealed class NotFoundException(string message) : JobfinderException(message);

public sealed class InvalidRequestException(string message) : JobfinderException(message);

public sealed class ConflictException(string message) : JobfinderException(message);

/// <summary>
/// Thrown when an operation needs the active user's data directory but first-run setup has not been
/// completed yet (no bootstrap config, and no email resolvable from git/env). Mapped to HTTP 428 so
/// the GUI can route the user to the setup screen.
/// </summary>
public sealed class SetupRequiredException(string message) : JobfinderException(message);
