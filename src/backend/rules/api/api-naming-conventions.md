# API Endpoint Naming Conventions

URI structure and naming conventions for all WebAPI endpoints.

## Base URL

All endpoints are prefixed with `/api`.

## General Structure

```
/api/{resource}/{id}/{sub-resource}/{id}/{action}
```

- `{resource}`: plural noun, lowercase (e.g., `orders`, `customers`, `products`)
- `{id}`: resource identifier with type constraint (e.g., `{id:int}`)
- `{sub-resource}`: nested resource belonging to parent (e.g., `items`, `attachments`)
- `{action}`: optional operation name in kebab-case (e.g., `approve`, `request-review`)

---

## Hierarchical Resource Nesting

Sub-resources are nested under their parent using slashes. This models the "owns" relationship.

**Correct (hierarchical with slashes):**
```
/api/orders/{id:int}/items                  → items owned by this order
/api/customers/{id:int}/contacts            → contacts owned by this customer
/api/products/{id:int}/reviews              → reviews owned by this product
/api/customers/{id:int}/users/invitations   → user invitations owned by this customer
```

**Avoid (kebab-case for sub-resources):**
```
/api/orders/order-items                     → use slashes instead
/api/customers/user-invitations             → use slashes instead
```

### When to Use Hierarchical Nesting

Use hierarchical nesting when the parent **owns** the child resource:

| Parent Resource | Sub-Resources | Reason |
|----------------|---------------|--------|
| `/orders/{id:int}` | `/items`, `/attachments` | Orders own their items and attachments |
| `/customers/{id:int}` | `/contacts`, `/users/invitations` | Customers own their contacts and invitations |
| `/products/{id:int}` | `/reviews`, `/variants` | Products own their reviews and variants |

### When to Use Filter-Style Routes

Use filter-style routes for **cross-resource queries** where one resource doesn't own the other:

```
/api/orders/items/customer/{id:int}         → order items filtered by customer
/api/products/reviews/customer/{id:int}     → product reviews filtered by customer
```

This makes it clear what resource you're querying and what you're filtering by.

### Top-Level Access

Resources have top-level endpoints for global queries and by-ID access:

```
GET /api/orders                             → all orders (requires ReadAll permission)
GET /api/orders/{id:int}                    → specific order by ID
GET /api/orders/me                          → my orders
GET /api/orders/items                       → all order items
GET /api/orders/items/{id:int}              → specific order item by ID
```

---

## Route Parameter Constraints

Always use type constraints on ID parameters:

```
{id:int}           → integer ID (required)
{id:int?}          → optional integer ID
{type}             → string enum (no constraint needed)
```

---

## Use of `/me`

The keyword `/me` represents the authenticated user's context. Place it immediately after the resource:

```
GET /api/orders/me
GET /api/orders/items/me
GET /api/customers/me
GET /api/roles/assignments/me
GET /api/permissions/assignments/me
```

This placement aligns with Microsoft Graph guidelines and REST best practices.

---

## HTTP Methods

### GET - Read

```
GET /api/orders                             → list all
GET /api/orders/{id:int}                    → get by ID
GET /api/orders/{id:int}/items              → get sub-resources
GET /api/orders/me                          → get my resources
```

### POST - Create

```
POST /api/orders                            → create resource
POST /api/orders/{id:int}/items             → create sub-resource
POST /api/customers/{id:int}/invitations    → create invitation
```

### PUT - Replace/Upload

```
PUT /api/orders/{id:int}                    → replace resource
PUT /api/orders/{id:int}/document           → upload document
PUT /api/orders/{id:int}/attachments/{type} → upload typed attachment
```

### PATCH - Partial Update / State Transition

Use PATCH for workflow state transitions with action names:

```
PATCH /api/orders/{id:int}/draft            → update draft
PATCH /api/orders/{id:int}/submit           → submit for review
PATCH /api/orders/{id:int}/approve          → approve
PATCH /api/orders/{id:int}/request-review   → request review
PATCH /api/orders/{id:int}/finalize         → finalize
```

### DELETE - Remove

```
DELETE /api/orders/{id:int}
DELETE /api/orders/{id:int}/attachments/{type}
DELETE /api/customers/{id:int}/invitations/{invitationId:int}
```

**Note:** Use DELETE even for "soft delete" or "deactivate" operations. The HTTP method indicates the intent, not the implementation.

---

## Naming Conventions

### Primary Resources

Primary (top-level) resources use lowercase without hyphens:

```
/api/orders
/api/customers
/api/products
/api/roles
/api/permissions
/api/audit
/api/system
```

### Sub-resources

Sub-resources use slash nesting, not kebab-case:

```
/api/orders/items/applications              ✓ (not item-applications)
/api/customers/users/invitations            ✓ (not user-invitations)
/api/products/reviews/responses             ✓
```

### Action Names

Actions are verbs or verb phrases in kebab-case:

```
/approve
/submit
/draft
/finalize
/request-review
/request-votes
/save-completed
/activate
/inactivate
```

---

## Route Definition in Code

Each module owns a `public static class Routes` in `<Module>/Routes.cs` that declares every URL the module exposes as `const string` values. Sub-resources are nested as inner static classes. Literal URLs are anchored to the shared `ApiConstants.RouteBase` prefix so all module routes stay consistent.

The top-level `Mwt.Api/Routes/Routes.cs` defines `ApiRoutes` for API-level endpoints only (Health, Dev, Search). Domain modules never add to `ApiRoutes` — each module has its own `Routes` class.

```csharp
// Mwt.Economy/Routes.cs
using Mwt.Core.Contracts;

namespace Mwt.Economy;

public static class Routes
{
    public const string Tag = "Economy";

    public static class Transactions
    {
        public const string Base = $"{ApiConstants.RouteBase}/economy/transactions";
        public const string ById = $"{Base}/{{id:guid}}";
        public const string GetAll = Base;
        public const string GetById = ById;
        public const string Create = Base;
        public const string Update = ById;
        public const string Delete = ById;
        public const string GetClassification = $"{ById}/classification";
    }

    public static class Categories
    {
        public const string Base = $"{ApiConstants.RouteBase}/economy/categories";
        public const string ById = $"{Base}/{{id:guid}}";
        // ...
    }
}
```

```csharp
// Mwt.Api/Routes/Routes.cs — API-level only
public static class ApiRoutes
{
    public const string Base = ApiConstants.RouteBase;

    public static class Health
    {
        public const string Tag = "Health";
        public const string Base = $"{ApiRoutes.Base}/system/health";
        public const string Ping = $"{Base}/ping";
    }
}
```

**Benefits:**
- Each module is self-contained — its routes live next to its endpoints/handlers.
- Type-safe route references with compile-time `const` folding.
- Shared prefix via `ApiConstants.RouteBase` keeps versioning/mounting uniform.

**Note on relative literals inside a typed group:** Route suffixes appended to a typed `MapGroup(Routes.X.Base)` may use relative literal strings (e.g. `"/{id:guid}"`). The typed base prefix satisfies the no-hardcoded-URL rule — the literal suffix is only the part the group composes onto the typed base.

---

## Workflow Example

A typical resource with a lifecycle follows this pattern:

```
POST   /api/orders                              → create draft
PATCH  /api/orders/{id:int}/draft               → update draft
PATCH  /api/orders/{id:int}/submit              → submit for review
PATCH  /api/orders/{id:int}/approve             → approve
PATCH  /api/orders/{id:int}/activate            → activate
PATCH  /api/orders/{id:int}/inactivate          → deactivate

PUT    /api/orders/{id:int}/attachments/{type}  → upload attachment
GET    /api/orders/{id:int}/attachments          → list attachments
DELETE /api/orders/{id:int}/attachments/{type}   → remove attachment
```

---

## References

- [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)
- [Microsoft Azure API Design Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design)
