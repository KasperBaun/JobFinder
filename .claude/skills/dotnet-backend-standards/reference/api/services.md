# Services

Services contain **ALL business logic**. They are HTTP-agnostic and return domain types. **Max 300 lines.**

## Rules

| MUST | MUST NOT |
|------|----------|
| Return domain types (DTOs, bools, entities) | Return `IResult` |
| Inject your `DbContext` for data access | Accept `ClaimsPrincipal` (use extracted user/id) |
| Throw typed exceptions for errors | Access `HttpContext` |
| Log business events | Catch exceptions to return error codes |
| Stay under 300 lines | |

## Pattern

```csharp
public sealed class OrderService(
    AppDbContext dbContext,
    ICustomLogger logger,
    IAuditEventService auditService) : IOrderService
{
    public async Task<List<Order>> GetOrders()
    {
        return await dbContext.Orders
            .OrderBy(o => o.Name)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderById(Guid orderId)
    {
        return await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<Order> CreateOrder(CreateOrderDTO request)
    {
        if (await dbContext.Orders.AnyAsync(o => o.Name == request.Name))
            throw new ConflictException("Order name already exists");

        var order = new Order
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        logger.Info("Order created: {OrderId}", order.Id);
        return order;
    }

    public async Task<bool> DeleteOrder(Guid orderId)
    {
        var order = await dbContext.Orders.FindAsync(orderId);
        if (order is null)
            return false;

        dbContext.Orders.Remove(order);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync("Order deleted", orderId);
        return true;
    }
}
```

## Return Types

| Operation | Return Type | Handler Translation |
|-----------|-------------|---------------------|
| Get by ID | `T?` | `null` → 404, otherwise 200 |
| Get list | `List<T>` | 200 (mapped via `.ToDTO()`) |
| Create | `T` | 200 (mapped via `.ToDTO()`) |
| Update | `T` | 200 (mapped via `.ToDTO()`) |
| Delete | `bool` | `false` → 404, `true` → 200 |
| Workflow action | `ServiceResult` | `!Success` → 400, otherwise 200 |

## Exception Types

Services throw, the handler base class translates to HTTP responses:

```csharp
throw new NotFoundException("Order not found");           // → 404
throw new InvalidRequestException("Invalid status");      // → 400
throw new ConflictException("Name already exists");       // → 409
throw new ForbiddenException("Access denied");            // → 403
throw new UnauthorizedException("Not authenticated");     // → 401
```

## User Context

Services don't accept `ClaimsPrincipal`. The handler extracts the user first:

```csharp
// ❌ BAD
Task<OrderDTO> CreateOrder(ClaimsPrincipal user, CreateOrderDTO request);

// ✅ GOOD
Task<OrderDTO> CreateOrder(CreateOrderDTO request);
Task<OrderDTO> CreateOrder(User authenticatedUser, AuditUser auditUser, CreateOrderDTO request);
```

## Anti-Patterns

```csharp
// ❌ BAD: Service returns IResult
public async Task<IResult> CreateOrder(...)
{
    if (nameExists) return Results.BadRequest("Name exists");
    return Results.Ok(dto);
}

// ✅ GOOD: Service returns domain types, throws exceptions
public async Task<Order> CreateOrder(...)
{
    if (nameExists) throw new ConflictException("Name exists");
    return order;
}
```

## Related

- [handlers.md](handlers.md) - Handlers call services
- [../data-access/entity-framework.md](../data-access/entity-framework.md) - Database patterns
