# Authentication & Authorization

## Summary

JWT-based authentication with permission-based authorization using resource scoping. The system supports granular permission checks with scope hierarchy (User → Organization → Global).

## Rules

- ✅ Use JWT Bearer tokens for authentication
- ✅ Implement permission-based authorization (not role-based)
- ✅ Use resource-based authorization with scope hierarchy
- ✅ Store authentication secrets in environment variables or user secrets
- ✅ Apply authorization declaratively at endpoint level
- ✅ Use `.RequireAuthorization()` for simple authenticated checks
- ✅ Use `.RequireAuthorizationWithResource(permissionKey)` for permission checks
- ✅ Define permissions as constants in `Permissions` class
- ✅ Validate permissions against database using `PermissionAuthorizationHandler`

## Architecture

### Authentication Flow

```text
1. User submits credentials (email + password)
2. Server validates credentials via bcrypt
3. Server generates JWT access token (15 min) + refresh token (7 days)
4. Tokens stored in HTTP-only cookies (access-token, refresh-token)
5. CSRF token generated and stored in csrf-token cookie
6. Future requests include access token for authentication
```

### Authorization Flow

```text
1. Request arrives with JWT access token
2. Authentication middleware validates token and extracts claims
3. Endpoint filter checks if authorization required
4. If permission-based:
   a. Extract permission key (e.g., "Role.Edit")
   b. Extract resource scope (Global, Organization, or User)
   c. Query database for user's role assignments
   d. Check if user has permission at required scope level
   e. Allow/deny request
5. Handler executes if authorized
```

## Configuration

### Authentication Setup

**Location**: `MWT.Api/Configuration/AuthenticationConfig.cs`

```csharp
public static void AddAuthentication(WebApplicationBuilder builder)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
            };
        });
}
```

### Authorization Setup

**Location**: `MWT.Api/Configuration/AuthorizationConfig.cs`

```csharp
public static IServiceCollection AddAuthCore(this IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // Auto-generate one policy per permission
        foreach (var perm in Permissions.All)
            options.AddPolicy(perm,
                p => p.AddRequirements(new PermissionRequirement(perm)));

        // SystemAdmin-only policy for administrative endpoints
        options.AddPolicy("SystemAdminOnly",
            policy => policy.RequireRole(GlobalRoles.SystemAdmin));
    });

    services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddHttpContextAccessor();

    return services;
}
```

## Permission System

### Permission Definitions

**Location**: `MWT.DataAccess/Authorization/Permissions.cs`

```csharp
public static class Permissions
{
    public static class User
    {
        public const string View = "User.View";
        public const string Edit = "User.Edit";
        public const string Delete = "User.Delete";
    }

    public static class Role
    {
        public const string View = "Role.View";
        public const string Edit = "Role.Edit";
        public const string Assign = "Role.Assign";
    }

    public static class Organization
    {
        public const string View = "Organization.View";
        public const string Edit = "Organization.Edit";
        public const string ManageUsers = "Organization.ManageUsers";
    }

    public static class Resource
    {
        public const string View = "Resource.View";
        public const string Edit = "Resource.Edit";
        public const string Delete = "Resource.Delete";
        public const string Share = "Resource.Share";
    }

    // All permissions for seeding and policy registration
    public static readonly string[] All =
        [.. User.All, .. Role.All, .. Organization.All, .. Resource.All];
}
```

### Permission Authorization Handler

**Location**: `MWT.Api/Authorization/PermissionAuthorizationHandler.cs`

The handler:
1. Extracts user ID from claims
2. Builds scope chain from resource (User → Organization → Global)
3. Queries database for user's role assignments
4. Checks if any assignment matches required scope + permission
5. Succeeds if permission found, fails otherwise

```csharp
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement, IAuthorizableResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        IAuthorizableResource resource)
    {
        var userId = context.User.GetUserId();
        var scopes = BuildScopeChain(resource); // User → Org → Global

        // Query database for user's role assignments
        var assignments = await db.RoleAssignments
            .Include(ra => ra.Role.RolePermissions)
            .Where(ra => ra.PrincipalId == userId)
            .ToListAsync();

        // Check if any assignment matches scope + permission
        var hasPermission = assignments
            .Where(ra => scopes.Any(s => s.Matches(ra)))
            .Any(ra => ra.Role.HasPermission(requirement.PermissionKey));

        if (hasPermission)
            context.Succeed(requirement);
    }
}
```

## Endpoint Authorization

### Simple Authentication (Any User)

For endpoints that only need to verify user is authenticated:

```csharp
app.MapGet(ApiRoutes.Users.Me,
    async ([FromServices] IUserHandler handler, ClaimsPrincipal user) =>
        await handler.GetCurrentUserAsync(user))
    .RequireAuthorization()  // ← No policy parameter needed
    .WithTags("Users");
```

### Permission-Based Authorization

For endpoints requiring specific permissions with Global scope:

```csharp
app.MapGet(ApiRoutes.Roles.Base,
    async ([FromServices] IRoleHandler handler, ClaimsPrincipal user) =>
        await handler.GetRolesAsync(user))
    .RequireAuthorizationWithResource("Role.View")  // ← Permission key
    .WithTags("Roles");
```

### Permission-Based with Custom Resource

For endpoints requiring permissions on specific resources:

```csharp
app.MapPut(ApiRoutes.Documents.ById,
    async ([FromServices] IDocumentHandler handler, Guid id, UpdateDocumentDto dto) =>
        await handler.UpdateDocumentAsync(id, dto))
    .RequireAuthorizationWithResource(
        DefaultAuthorizableResource.Organization(organizationId),
        "Resource.Edit")
    .WithTags("Documents");
```

### Permission-Based with Organization from Claims

For endpoints that should use the user's organization from claims:

```csharp
app.MapPost(ApiRoutes.Reports.Generate,
    async ([FromServices] IReportHandler handler, ClaimsPrincipal user) =>
        await handler.GenerateReportAsync(user))
    .RequireAuthorizationWithOrganizationResource("Resource.View")
    .WithTags("Reports");
```

### Role-Based (Rare)

Only use for system administrator checks:

```csharp
app.MapDelete(ApiRoutes.System.ClearCache,
    async ([FromServices] ICacheService cache) =>
        await cache.ClearAllAsync())
    .RequireAuthorization("SystemAdminOnly")  // ← Only system admins
    .WithTags("System");
```

## Resource Scoping

### Scope Hierarchy

Permissions are evaluated in hierarchical order:

```text
User Scope (most specific)
    ↓
Organization Scope
    ↓
Global Scope (least specific)
```

**Example**: If a user has "Role.Edit" permission at Organization scope, they can edit roles within their organization, but not globally.

### IAuthorizableResource

Resources must implement this interface for scope-based authorization:

```csharp
public interface IAuthorizableResource
{
    ScopeType OwnerScopeType { get; }
    Guid? OwnerScopeId { get; }
}
```

### Default Resources

Use these for common scenarios:

```csharp
// Global scope (all users)
var resource = DefaultAuthorizableResource.Global();

// Organization scope
var resource = DefaultAuthorizableResource.Organization(organizationId);

// User scope
var resource = DefaultAuthorizableResource.User(userId);
```

## Claims Helper Extensions

**Location**: `MWT.Api/Extensions/ClaimsPrincipalExtensions.cs`

```csharp
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim);
    }

    public static string GetUserEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    public static string GetOrganization(this ClaimsPrincipal user)
    {
        return user.FindFirst("organization")?.Value;
    }

    public static IEnumerable<Guid> GetGroupIds(this ClaimsPrincipal user)
    {
        return user.FindAll("group")
            .Select(c => Guid.Parse(c.Value));
    }
}
```

## Database Schema

### Core Entities

- **User**: User accounts with email, password hash, and profile info
- **Role**: Named collections of permissions (e.g., "Content Editor", "Organization Admin")
- **Permission**: Individual permission keys (e.g., "User.Edit", "Role.View")
- **RolePermission**: Many-to-many relationship between roles and permissions
- **RoleAssignment**: Assigns roles to users/groups at specific scopes

### Role Assignment Model

```csharp
public class RoleAssignment
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }  // Which role
    public PrincipalType PrincipalType { get; set; }  // User or Group
    public Guid PrincipalId { get; set; }  // Which user/group
    public ScopeType ScopeType { get; set; }  // Global, Organization, User
    public Guid? ScopeId { get; set; }  // Scope identifier

    public Role Role { get; set; }
}
```

### Scope Types

```csharp
public enum ScopeType
{
    Global = 0,        // System-wide permissions
    Organization = 1,  // Organization-level permissions
    User = 2          // User-specific permissions
}
```

## Security Configuration

### JWT Settings

**Location**: `appsettings.json` (DO NOT commit secrets!)

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-chars",
    "Issuer": "modernweb-template-api",
    "Audience": "modernweb-template-client",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

**For production, use environment variables or Azure Key Vault!**

### Cookie Configuration

```csharp
httpContext.Response.Cookies.Append("access-token", accessToken, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTimeOffset.UtcNow.AddMinutes(15)
});
```

## Best Practices

### DO

✅ Use `.RequireAuthorization()` for simple authenticated checks
✅ Use `.RequireAuthorizationWithResource(permission)` for permission checks
✅ Define all permissions as constants in `Permissions` class
✅ Use scope hierarchy to minimize permission assignments
✅ Validate permissions at endpoint level (declarative)
✅ Use HTTP-only cookies for token storage
✅ Implement CSRF protection for cookie-based auth

### DON'T

❌ Check permissions manually in handlers (use endpoint filters)
❌ Use magic strings for permission keys
❌ Create custom policies manually (auto-generated from Permissions.All)
❌ Store tokens in localStorage (XSS vulnerability)
❌ Skip CSRF protection on state-changing operations
❌ Use role-based authorization (use permissions instead)

## Testing

### Integration Tests

```csharp
[Fact]
public async Task GetRoles_WithRoleViewPermission_ReturnsRoles()
{
    // Arrange
    var user = await CreateUserWithPermissionAsync("Role.View", ScopeType.Global);
    var client = CreateAuthenticatedClient(user);

    // Act
    var response = await client.GetAsync("/api/v1/roles");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task GetRoles_WithoutRoleViewPermission_ReturnsForbidden()
{
    // Arrange
    var user = await CreateUserWithoutPermissionsAsync();
    var client = CreateAuthenticatedClient(user);

    // Act
    var response = await client.GetAsync("/api/v1/roles");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

## Migration from Role-Based to Permission-Based

If migrating from old role-based authorization:

1. **Remove role-based policies** from AuthorizationConfig
2. **Replace** `.RequireAuthorization("RolePolicy")` with `.RequireAuthorizationWithResource("Permission.Key")`
3. **Update** handlers to not check roles directly
4. **Seed** permissions and role assignments in database
5. **Test** all endpoints with permission-based authorization

## Related Files

- `MWT.Api/Configuration/AuthenticationConfig.cs` - JWT authentication setup
- `MWT.Api/Configuration/AuthorizationConfig.cs` - Policy registration
- `MWT.Api/Authorization/PermissionAuthorizationHandler.cs` - Permission evaluation
- `MWT.Api/Authorization/PermissionRequirement.cs` - Authorization requirement
- `MWT.Api/Endpoints/Infrastructure/ResourceAuthorizationFilter.cs` - Endpoint extensions
- `MWT.DataAccess/Authorization/Permissions.cs` - Permission definitions
- `MWT.DataAccess/Entities/Authorization/` - Authorization entities

## Related Documentation

- [API Endpoints](../api/api-endpoints.md) - Endpoint authorization patterns
- [Authorization User Guide](/documentation/development/backend/authorization/user-guide.md) - Detailed authorization guide
- [Authorization API Reference](/documentation/development/backend/authorization/api-reference.md) - API documentation

---

**Last Updated**: 2025-01-24
**Maintainer**: Backend Team
