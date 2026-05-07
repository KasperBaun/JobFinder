namespace Jobmatch;

public abstract class JobfinderException(string message) : Exception(message);

public sealed class NotFoundException(string message) : JobfinderException(message);

public sealed class InvalidRequestException(string message) : JobfinderException(message);

public sealed class ConflictException(string message) : JobfinderException(message);
