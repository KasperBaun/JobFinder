# API Endpoints

Endpoints are **route definitions only**. They configure routing, OpenAPI docs, authorization, and validation. **No business logic**

## Rules

| MUST | MUST NOT |
|------|----------|
| Use typed routes from `Routes.*` | Contain business logic |
| Implement `IEndpointRegistration` | Access database |
| Delegate to handlers via `[FromServices]` | Inject services directly |
| Extract each endpoint into a `private static` method | Have conditional logic |
| Include full OpenAPI docs | Do manual validation |
| Apply `.RequirePermission()` per endpoint | |

## Pattern

```csharp
public sealed class OrderEndpoints : IEndpointRegistration
{
    public void Register(WebApplication app)
    {
        var group = app.MapGroup("")
            .WithTags(nameof(Routes.Order));

        MapGetOrders(group);
        MapGetOrderById(group);
        MapCreateOrder(group);
    }

    private static void MapGetOrders(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Order.GetAll,
                (
                    [FromServices] IOrderHandler handler,
                    ClaimsPrincipal claimsPrincipal
                ) => handler.GetOrders(claimsPrincipal))
           .WithName($"{nameof(Routes.Order)}.{nameof(Routes.Order.GetAll)}")
           .WithSummary("Get Orders")
           .WithDescription(
               OpenApiSpecifications.RequiredPermission(Permissions.Order.ReadAll) +
               "Returns a list of all orders.")
           .Produces<List<OrderDTO>>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status401Unauthorized)
           .Produces(StatusCodes.Status403Forbidden)
           .Produces(StatusCodes.Status500InternalServerError)
           .RequirePermission(Permissions.Order.ReadAll);
    }

    private static void MapGetOrderById(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Order.GetById,
                (
                    [FromServices] IOrderHandler handler,
                    ClaimsPrincipal claimsPrincipal,
                    [FromRoute] int id
                ) => handler.GetOrderById(claimsPrincipal, id))
           .WithName($"{nameof(Routes.Order)}.{nameof(Routes.Order.GetById)}")
           .WithSummary("Get Order By Id")
           .WithDescription(
               OpenApiSpecifications.RequiredPermission(Permissions.Order.Read) +
               "Returns a single order by its identifier.")
           .Produces<OrderDTO>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status400BadRequest)
           .Produces(StatusCodes.Status401Unauthorized)
           .Produces(StatusCodes.Status403Forbidden)
           .Produces(StatusCodes.Status404NotFound)
           .Produces(StatusCodes.Status500InternalServerError)
           .RequirePermission(Permissions.Order.Read, ScopeResolverType.Order);
    }

    private static void MapCreateOrder(RouteGroupBuilder group)
    {
        group.MapPost(
                Routes.Order.Create,
                (
                    [FromServices] IOrderHandler handler,
                    ClaimsPrincipal claimsPrincipal,
                    [FromBody] CreateOrderDTO request
                ) => handler.CreateOrder(claimsPrincipal, request))
           .WithName($"{nameof(Routes.Order)}.{nameof(Routes.Order.Create)}")
           .WithSummary("Create Order")
           .WithDescription(
               OpenApiSpecifications.RequiredPermission(Permissions.Order.Create) +
               "Creates a new order.")
           .Produces<OrderDTO>(StatusCodes.Status200OK)
           .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
           .Produces(StatusCodes.Status401Unauthorized)
           .Produces(StatusCodes.Status403Forbidden)
           .Produces(StatusCodes.Status500InternalServerError)
           .RequirePermission(Permissions.Order.Create);
    }
}
```

## Endpoint Structure

Each endpoint class follows this structure:
1. **`Register()`** - creates a group and calls private static methods
2. **Private static methods** - one per endpoint, named `Map{Operation}` (e.g., `MapGetOrders`, `MapCreateOrder`)
3. **Fluent chain** - route, handler delegation, OpenAPI metadata, authorization

## Route References

Always use typed route properties from the `Routes` class:

```csharp
// ✅ GOOD: Typed route reference
Routes.Order.GetAll          // resolves to "api/orders"
Routes.Order.GetById         // resolves to "api/orders/{id:int}"

// ❌ BAD: Hardcoded string
"api/orders"
"api/orders/{id:int}"
```

## OpenAPI Requirements

Every endpoint must have:
```csharp
.WithName($"{nameof(Routes.Order)}.{nameof(Routes.Order.GetAll)}")  // Unique operation ID using nameof
.WithSummary("Short Title")                                          // Brief description
.WithDescription(
    OpenApiSpecifications.RequiredPermission(Permissions.Order.Read) + // Permission documentation
    "Detailed description of the endpoint.")                           // Functional description
.Produces<T>(StatusCode)                                              // All possible responses
```

## Authorization

```csharp
// Permission-based (most common)
.RequirePermission(Permissions.Order.Read)

// Permission with scope resolver
.RequirePermission(Permissions.Order.Read, ScopeResolverType.Order)

// Anonymous endpoints (token/link-based)
// Do NOT add .RequirePermission()
```

## Parameter Binding Order

```csharp
(
    [FromServices] IHandler handler,     // 1. Services
    ClaimsPrincipal claimsPrincipal,     // 2. User context
    [FromRoute] int id,                  // 3. Route params
    [FromQuery] int page = 1,            // 4. Query params
    [FromBody] CreateDto request         // 5. Body last
)
```

## Anti-Patterns

```csharp
// ❌ BAD: Logic in endpoint
.MapPost("", async (DbContext db, CreateOrderDTO request) =>
{
    var order = new Order { Name = request.Name };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created(...);
})

// ❌ BAD: Inline endpoint without private static method
public void Register(WebApplication app)
{
    app.MapGet(Routes.Order.GetAll, ([FromServices] IOrderHandler handler) =>
        handler.GetOrders());
}

// ✅ GOOD: Private static method delegating to handler
private static void MapGetOrders(RouteGroupBuilder group)
{
    group.MapGet(
            Routes.Order.GetAll,
            ([FromServices] IOrderHandler handler, ClaimsPrincipal claimsPrincipal)
                => handler.GetOrders(claimsPrincipal))
       .WithName(...)
       .WithSummary(...)
       // ...
}
```

## Related

- [handlers.md](handlers.md) - Handler patterns
- [services.md](services.md) - Service layer
- [api-naming-conventions.md](api-naming-conventions.md) - URI naming conventions
- [../security/auth.md](../security/auth.md) - Authorization setup
