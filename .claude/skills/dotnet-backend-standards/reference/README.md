# Backend Development Rules

Full rule set for a layered .NET backend. Names like `HandlerBase` are **reference names**
— map them to their jobfinder equivalents. **Jobfinder-specific carve-outs live in the
parent [`SKILL.md`](../SKILL.md)** ("Jobfinder deviations from the generic rules") —
that section wins whenever this file suggests EF Core, auth, GUID keys, or SQL Server.

## Architecture

```
Endpoint → Handler → Service          (jobfinder: no DbContext layer)
   ↓          ↓          ↓
OpenAPI    Logging    Business
           Result     Logic + file I/O
           Translation
```

| Layer | Role | Returns | Max Lines |
|-------|------|---------|-----------|
| Endpoints | HTTP routing, OpenAPI | delegates to handler | 300 |
| Handlers | Orchestration, logging, result translation | `IResult` (via base wrapper) | 300 |
| Services | Business logic, file-backed data access (via stores) | DTOs / immutable records / bools | 300 |

## What Goes Where

| Concern | Endpoint | Handler | Service |
|---------|----------|---------|---------|
| Routes, OpenAPI | ✅ | | |
| Validation filters | ✅ | | |
| Operation logging | | ✅ | |
| Result → `IResult` translation | | ✅ | |
| Business logic | | | ✅ |
| File-backed data access (stores) | | | ✅ |

## Rule Files

### API
- [api/api-endpoints.md](api/api-endpoints.md) — Route definitions, OpenAPI (**ignore `.RequirePermission*` guidance — jobfinder has no auth**)
- [api/handlers.md](api/handlers.md) — Thin orchestration, logging, result translation
- [api/services.md](api/services.md) — Business logic, data access (**swap `DbContext` for a file-backed store**)
- [api/api-naming-conventions.md](api/api-naming-conventions.md) — URI structure, HTTP methods, route naming, `Endpoints/Routes.cs`
- [api/openapi.md](api/openapi.md) — OpenAPI document (AddOpenApi), per-module tags, Scalar UI, versioning

### Conventions
- [conventions/coding-conventions.md](conventions/coding-conventions.md) — C# standards (collection expressions, `.Count > 0`, enums, primary constructors)
- [conventions/maintainability.md](conventions/maintainability.md) — Size limits and refactoring triggers
- [conventions/refactoring-strategy.md](conventions/refactoring-strategy.md) — Partial classes vs service extraction

### Data Access

> **Jobfinder note:** the generic EF Core / GUID-id rule files are removed — jobfinder uses
> JSON files under `data/<email>/` (SQLite only for Hangfire) and string run-ids. See
> the parent `SKILL.md` → *"Jobfinder deviations from the generic rules"*.

### Infrastructure
- [infrastructure/dependency-injection.md](infrastructure/dependency-injection.md) — Module registration, lifetimes, ctor limits
- [infrastructure/configuration.md](infrastructure/configuration.md) — Strongly-typed options, fail-fast validation
- [infrastructure/logging.md](infrastructure/logging.md) — Structured logging
- [infrastructure/background-jobs.md](infrastructure/background-jobs.md) — Job scheduling, retries (**jobfinder overrides: SQLite storage, unsecured dashboard, `Attempts = 1` for the search job**)

### Security

> **Jobfinder note:** `security/auth.md` is removed — jobfinder has **no auth**. Never add
> `.RequirePermission*(...)`, `[Authorize]`, or JWT wiring.

- [security/error-handling.md](security/error-handling.md) — Typed exceptions, exception → HTTP mapping

### Testing
- [testing/unit-tests.md](testing/unit-tests.md) — xUnit, hand-rolled fakes, AAA
- [testing/integration-tests.md](testing/integration-tests.md) — Real-database API testing (**jobfinder equivalent: scratch data dir, `AddJobmatchApi(enableBackgroundJobs: false)`**)
- [testing/e2e-tests.md](testing/e2e-tests.md) — End-to-end user journeys

## Quick Decision

```
HTTP routing / OpenAPI?            → Endpoint       (no auth wiring in jobfinder)
Logging / result translation?      → Handler
Business logic / file I/O?         → Service        (via a store, no DbContext)
```

## Anti-Patterns

```csharp
// ❌ Handler with a store injected
public class HistoryHandler(HistoryStore db) { }

// ✅ Handler with a service
public class HistoryHandler(IHistoryService svc) { }

// ❌ Service returning IResult
public Task<IResult> CreateAsync() { }

// ✅ Service returning a DTO / immutable record
public Task<HistoryEntry> CreateAsync() { }

// ❌ Endpoint requiring a permission (jobfinder has no auth)
group.MapGet(...).RequirePermission(Permissions.HistoryRead);

// ✅ Endpoint with only OpenAPI + Produces metadata
group.MapGet(...).Produces<HistoryEntry>();
```
