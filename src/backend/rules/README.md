# Backend Development Rules

## Architecture

```
Endpoint → Handler → Service → DbContext
   ↓          ↓          ↓
OpenAPI    Logging    Business
Auth       IResult    Logic
Filters    Translation
```

| Layer | Location | Returns | Max Lines |
|-------|----------|---------|-----------|
| Endpoints | `Mwt.Api/Endpoints/` | Delegates | 200 |
| Handlers | `Mwt.Api/Handlers/` | `IResult` | 300 |
| Services | `Mwt.Domain/Services/` | DTOs, bools | 300 |

## What Goes Where

| Concern | Endpoint | Handler | Service |
|---------|----------|---------|---------|
| Routes, OpenAPI | ✅ | | |
| Authorization | ✅ | | |
| Validation filters | ✅ | | |
| Operation logging | | ✅ | |
| Result → IResult | | ✅ | |
| Business logic | | | ✅ |
| Database access | | | ✅ |
| Audit logging | | | ✅ |

## Rule Files

### API
- [api/api-endpoints.md](api/api-endpoints.md) - Route definitions, OpenAPI, auth
- [api/handlers.md](api/handlers.md) - Thin orchestration, logging, result translation
- [api/services.md](api/services.md) - Business logic, data access

### Other
- [conventions/coding-conventions.md](conventions/coding-conventions.md) - C# standards
- [conventions/maintainability.md](conventions/maintainability.md) - Size limits
- [data-access/entity-framework.md](data-access/entity-framework.md) - EF Core patterns
- [infrastructure/dependency-injection.md](infrastructure/dependency-injection.md) - DI patterns
- [security/auth.md](security/auth.md) - JWT, permissions
- [security/error-handling.md](security/error-handling.md) - Exception handling
- [testing/unit-tests.md](testing/unit-tests.md) - Unit testing
- [testing/integration-tests.md](testing/integration-tests.md) - API testing

## Quick Decision

```
HTTP routing/OpenAPI/auth?     → Endpoint
Logging/result translation?    → Handler
Business logic/database?       → Service
```

## Anti-Patterns

```csharp
// ❌ Handler with DbContext
public class GroupHandler(MWTDbContext db) { }

// ✅ Handler with Service
public class GroupHandler(IGroupService svc) { }

// ❌ Service returning IResult
public Task<IResult> CreateAsync() { }

// ✅ Service returning DTO
public Task<GroupDto> CreateAsync() { }
```
