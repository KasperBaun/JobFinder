# Error Handling

## Summary

Centralized error handling via the base `ExecuteAsync` wrapper on your handler base class for consistent API behavior. All exceptions are caught and mapped to appropriate HTTP responses in one place.

## Rules

- Use the base `ExecuteAsync` wrapper for all handler methods (handles exceptions automatically)
- Use custom exception types for domain-specific errors
- Log exceptions via your structured logger abstraction (done automatically by the handler base class)
- Services throw exceptions, handlers let the handler base class translate them
- Do NOT catch exceptions in individual handler methods
- Do NOT use middleware for exception handling (handled in the handler base class)

## Exception → HTTP Mapping

The handler base class maps exceptions to HTTP responses:

| Exception | HTTP Status | Response |
|-----------|-------------|----------|
| `NotFoundException` | 404 | `Results.NotFound(message)` |
| `InvalidRequestException` | 400 | `Results.BadRequest(message)` |
| `UnauthorizedException` | 401 | `Results.Unauthorized()` |
| `ForbiddenException` | 403 | `Results.Forbid()` |
| `ConflictException` | 409 | `Results.Conflict(message)` |
| `InternalDataInvalidException` | 500 | `Results.Problem(detail, title)` |
| `ValidationException` (FluentValidation) | 400 | `Results.ValidationProblem(errors)` |
| Any other `Exception` | 500 | `Results.Problem(detail, title)` |

## Custom Exception Types

All domain exceptions inherit from a common base:

```csharp
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

public class InvalidRequestException : AppException
{
    public InvalidRequestException(string message) : base(message) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message) { }
}
```

## How It Works

The base `ExecuteAsync` method wraps all handler operations:

```csharp
protected async Task<IResult> ExecuteAsync(
    string operationName,
    Func<Task<IResult>> operation,
    params object[] logParams)
{
    _logger.Info($"Starting {operationName}", logParams);
    try
    {
        var result = await operation();
        _logger.Info($"Completed {operationName}", logParams);
        return result;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, $"Error during {operationName}", logParams);
        return ex switch
        {
            NotFoundException notFound => Results.NotFound(notFound.Message),
            InvalidRequestException invalid => Results.BadRequest(invalid.Message),
            UnauthorizedException => Results.Unauthorized(),
            ForbiddenException => Results.Forbid(),
            ConflictException conflict => Results.Conflict(conflict.Message),
            ValidationException validation => Results.ValidationProblem(...),
            _ => Results.Problem(detail: ex.Message, statusCode: 500, title: "An unexpected error occurred")
        };
    }
}
```

## Anti-Patterns

```csharp
// ❌ BAD: Manual try/catch in handler method
public async Task<IResult> GetOrderById(ClaimsPrincipal user, Guid id)
{
    try
    {
        var order = await orderService.GetOrderById(id);
        return Results.Ok(order.ToDTO());
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
}

// ✅ GOOD: Let the handler base class handle exceptions
public Task<IResult> GetOrderById(ClaimsPrincipal claimsPrincipal, Guid id)
    => ExecuteAsync(
        "get order: {OrderId}",
        async () =>
        {
            var order = await orderService.GetOrderById(id);
            return Results.Ok(order.ToDTO());
        },
        logParams: [id]);

// ❌ BAD: Service catching exceptions to return error codes
public async Task<IResult> CreateOrder(CreateOrderDTO request)
{
    if (nameExists) return Results.BadRequest("Name exists");
    return Results.Ok(dto);
}

// ✅ GOOD: Service throws, the handler base class translates
public async Task<Order> CreateOrder(CreateOrderDTO request)
{
    if (nameExists) throw new ConflictException("Name already exists");
    return order;
}
```

## Cross-actor / cross-tenant access — 403 vs 404 convention

When a caller authenticates successfully but targets a resource that belongs to another
user / tenant, modules choose between two correct responses. Pick deliberately per
module and document the choice.

| Module pattern | Response | When to use |
|---|---|---|
| **404 (info-hiding)** — service filters by `(id, ownerId)` and throws `NotFoundException` when no row matches | preferred for personal resources where existence itself is private (e.g. private conversations, personal prompts, notifications) | leaks nothing about whether the resource exists |
| **403 (explicit deny)** — an access guard runs before the service body and throws `ForbiddenException` | preferred for shared resources where the resource is known to exist but the caller isn't entitled to it (e.g. files / folders inside an inaccessible shared mount) | gives legitimate callers a clear "you need access" signal for debugging permission issues |

**Don't mix patterns within one module.** Pick one per resource type and pin it with
multi-tenant tests in your integration test suite.

## Notes

- Always log errors with context information (structured logging placeholders)
- Don't expose internal implementation details in error messages
- Use `InternalDataInvalidException` for data integrity issues that indicate a bug
- Use `ValidationException` (FluentValidation) for input validation failures

## Related Documentation

- [Handlers](../api/handlers.md) - Handler base class and the `ExecuteAsync` wrapper
- [Authentication & Authorization](./auth.md) - 401/403 authorization responses
