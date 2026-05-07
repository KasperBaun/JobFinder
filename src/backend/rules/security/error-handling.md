# Error Handling

## Summary

Centralized error handling via `HandlerBase.ExecuteAsync()` for consistent API behavior. All exceptions are caught and mapped to appropriate HTTP responses in one place.

## Rules

- Use `HandlerBase.ExecuteAsync()` for all handler methods (handles exceptions automatically)
- Use custom exception types for domain-specific errors
- Log exceptions via `ICustomLogger` (done automatically by `HandlerBase`)
- Services throw exceptions, handlers let `HandlerBase` translate them
- Do NOT catch exceptions in individual handler methods
- Do NOT use middleware for exception handling (handled in `HandlerBase`)

## Exception → HTTP Mapping

`HandlerBase` maps exceptions to HTTP responses:

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

The `HandlerBase.ExecuteAsync()` method wraps all handler operations:

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
public async Task<IResult> GetOrderById(ClaimsPrincipal user, int id)
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

// ✅ GOOD: Let HandlerBase handle exceptions
public Task<IResult> GetOrderById(ClaimsPrincipal claimsPrincipal, int id)
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

// ✅ GOOD: Service throws, HandlerBase translates
public async Task<Order> CreateOrder(CreateOrderDTO request)
{
    if (nameExists) throw new ConflictException("Name already exists");
    return order;
}
```

## Notes

- Always log errors with context information (structured logging placeholders)
- Don't expose internal implementation details in error messages
- Use `InternalDataInvalidException` for data integrity issues that indicate a bug
- Use `ValidationException` (FluentValidation) for input validation failures
