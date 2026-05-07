# Handlers

Handlers are **thin orchestration layers** with exactly three jobs:
1. **Log** via `HandlerBase.ExecuteAsync()`
2. **Call** domain services
3. **Return** `IResult` (mapping DTOs inside the `ExecuteAsync` lambda)

**No business logic. No database access. Max 300 lines.**

## Rules

| MUST | MUST NOT |
|------|----------|
| Inherit `HandlerBase` | Inject `DbContext` |
| Use `ExecuteAsync()` for every method | Contain business logic |
| Return `IResult` inside the lambda | Create/manipulate entities |
| Use `.ToDTO()` extension methods for mapping | Have complex conditionals |
| Use structured `logParams` for identifiers | Catch exceptions manually |
| Stay under 300 lines | Log business events (services do this) |

## Pattern

```csharp
public sealed class OrderHandler(
    IOrderService orderService,
    ICustomLogger logger) : HandlerBase(logger), IOrderHandler
{
    public Task<IResult> GetOrders(ClaimsPrincipal claimsPrincipal)
        => ExecuteAsync(
            "get all orders",
            async () =>
            {
                var orders = await orderService.GetOrders();
                var dtos = orders.Select(x => x.ToDTO());
                return Results.Ok(dtos);
            });

    public Task<IResult> GetOrderById(ClaimsPrincipal claimsPrincipal, int orderId)
        => ExecuteAsync(
            "get order: {OrderId}",
            async () =>
            {
                var order = await orderService.GetOrderById(orderId);
                return Results.Ok(order.ToDTO());
            },
            logParams: [orderId]);

    public Task<IResult> CreateOrder(ClaimsPrincipal claimsPrincipal, CreateOrderDTO request)
        => ExecuteAsync(
            "create order",
            async () =>
            {
                var order = await orderService.CreateOrder(request);
                return Results.Ok(order.ToDTO());
            });
}
```

## ExecuteAsync

`HandlerBase.ExecuteAsync()` provides consistent logging and exception handling:

```csharp
protected async Task<IResult> ExecuteAsync(
    string operationName,          // Structured log message (e.g., "get order: {OrderId}")
    Func<Task<IResult>> operation, // The handler logic returning IResult
    params object[] logParams)     // Values for structured logging placeholders
```

- Logs "Starting {operationName}" and "Completed {operationName}" automatically
- Catches all exceptions and maps them to HTTP responses (see Exception Handling below)
- Use `logParams:` named parameter for identifier values

## Exception Handling

Exceptions are handled centrally in `HandlerBase`, **not** in individual handlers or middleware. Handlers do **NOT** catch exceptions.

| Exception | HTTP Response |
|-----------|---------------|
| `NotFoundException` | 404 Not Found |
| `InvalidRequestException` | 400 Bad Request |
| `UnauthorizedException` | 401 Unauthorized |
| `ForbiddenException` | 403 Forbidden |
| `ConflictException` | 409 Conflict |
| `InternalDataInvalidException` | 500 Problem |
| `ValidationException` (FluentValidation) | 400 ValidationProblem |
| Any other `Exception` | 500 Problem |

## Multi-Service Orchestration

Handlers may coordinate multiple services, but the logic stays thin:

```csharp
public Task<IResult> ApproveOrder(ClaimsPrincipal claimsPrincipal, int orderId)
    => ExecuteAsync(
        "approve order: {OrderId}",
        async () =>
        {
            var user = await claimsPrincipal.GetAuthenticatedUserAsync(userService, db);
            var auditUser = await auditUserService.GetOrCreateAsync(db, claimsPrincipal);
            await db.SaveChangesAsync();

            var result = await orderService.ApproveAsync(orderId, user, auditUser);

            if (!result.Success)
                return Results.BadRequest(result.ErrorMessage);

            return Results.Ok();
        },
        logParams: [orderId]);
```

## Handler Interface

Each handler has a corresponding interface for DI:

```csharp
public interface IOrderHandler
{
    Task<IResult> GetOrders(ClaimsPrincipal claimsPrincipal);
    Task<IResult> GetOrderById(ClaimsPrincipal claimsPrincipal, int orderId);
    Task<IResult> CreateOrder(ClaimsPrincipal claimsPrincipal, CreateOrderDTO request);
}
```

## Anti-Patterns

```csharp
// ❌ BAD: Business logic in handler
public async Task<IResult> CreateOrder(...)
{
    var exists = await _dbContext.Orders.AnyAsync(o => o.Name == request.Name);
    if (exists) return Results.BadRequest("Name exists");
    var order = new Order { Name = request.Name };
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync();
    return Results.Created(...);
}

// ❌ BAD: Manual try/catch in handler
public async Task<IResult> GetOrder(ClaimsPrincipal user, int id)
{
    try { ... }
    catch (NotFoundException ex) { return Results.NotFound(ex.Message); }
}

// ✅ GOOD: Thin handler using ExecuteAsync
public Task<IResult> CreateOrder(ClaimsPrincipal claimsPrincipal, CreateOrderDTO request)
    => ExecuteAsync(
        "create order",
        async () =>
        {
            var order = await orderService.CreateOrder(request);
            return Results.Ok(order.ToDTO());
        });
```

## Related

- [api-endpoints.md](api-endpoints.md) - Endpoint configuration
- [services.md](services.md) - Service layer patterns
