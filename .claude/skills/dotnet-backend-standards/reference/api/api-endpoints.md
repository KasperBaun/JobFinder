# API Endpoints

Endpoints are **route definitions only**. They configure routing, OpenAPI docs, authorization, and validation. **No business logic**

## Rules

| MUST | MUST NOT |
|------|----------|
| Use typed route constants (`Routes.*`) | Contain business logic |
| Implement `IEndpointRegistration` | Access database |
| Delegate to handlers via `[FromServices]` | Inject services directly |
| Extract each endpoint into a `private static Map{Operation}(group)` method | Have conditional logic |
| Keep `Register()` to group setup + one call per `Map` method | Register routes inline in `Register()` |
| Include full OpenAPI docs | Do manual validation |
| Apply `.RequirePermissionGlobal(perm)` or scoped `.RequirePermission(perm, scope, route)` per endpoint | Use `.RequirePermissionGlobal` on per-resource endpoints — `{id}` routes need the scoped overload (or a domain access filter) to prevent cross-resource leaks |

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
           .RequirePermissionGlobal(Permissions.Order.ReadAll);
    }

    private static void MapGetOrderById(RouteGroupBuilder group)
    {
        group.MapGet(
                Routes.Order.GetById,
                (
                    [FromServices] IOrderHandler handler,
                    ClaimsPrincipal claimsPrincipal,
                    [FromRoute] Guid id
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
           .RequirePermission(Permissions.Order.Read, (int)ScopeResolverType.Order, "id");
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
           .RequirePermissionGlobal(Permissions.Order.Create);
    }
}
```

## Endpoint Structure

Each endpoint class follows this structure — no exceptions:
1. **`Register()`** - builds the route group and does nothing but call the `Map` methods. It contains **no route registrations of its own** (no `MapGet`/`MapPost`/… inline) and no logic. One line per endpoint: `MapGetOrders(group);`, `MapCreateOrder(group);`, …
2. **Private static methods** - exactly one per endpoint, named `Map{Operation}` (e.g., `MapGetOrders`, `MapCreateOrder`). Each registers a single route via its `group` parameter.
3. **Fluent chain** - route, handler delegation, OpenAPI metadata, authorization

If a route is registered inline in `Register()`, or one `Map` method registers more than one route, the file violates the standard — split it.

## Route References

Always use typed route properties from your typed route constants class (`Routes`):

```csharp
// ✅ GOOD: Typed route reference
Routes.Order.GetAll          // resolves to "api/orders"
Routes.Order.GetById         // resolves to "api/orders/{id:guid}"

// ❌ BAD: Hardcoded string
"api/orders"
"api/orders/{id:guid}"
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
// Global permission check (no per-resource scope) —
// use for platform-admin endpoints, /me-style endpoints,
// or any route without an `{id}` / `{orgId}` route value.
.RequirePermissionGlobal(Permissions.Order.ReadAll)

// Scoped permission check — pass the resolver type as int and the
// route parameter name to extract the resource ID from. Use for
// per-resource endpoints (`/orders/{id}`, `/organisations/{orgId}/…`)
// so that scope-limited grants cannot leak across resources.
.RequirePermission(Permissions.Order.Read, (int)ScopeResolverType.Order, "id")

// Anonymous endpoints (token/link-based)
// Do NOT add a .RequirePermission* extension.
```

**Heads-up:** the unscoped overload is named `RequirePermissionGlobal` *on purpose* —
a bare `RequirePermission(perm)` is a footgun on per-resource endpoints
because it never checks the scope of the grant. New code should reach for the
scoped overload (or a domain access filter such as `.RequireOrganisationAccess()`)
whenever the route includes a resource identifier.

## Parameter Binding Order

```csharp
(
    [FromServices] IHandler handler,     // 1. Services
    ClaimsPrincipal claimsPrincipal,     // 2. User context
    [FromRoute] Guid id,                  // 3. Route params
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
