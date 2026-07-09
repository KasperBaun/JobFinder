---
name: dotnet-backend-standards
description: Use when adding, modifying, or refactoring C# code in jobfinder's backend — endpoints, handlers, services, adapters, DI registration, modules, or backend tests — and when verifying that backend work is complete before committing, opening a PR, or marking a task done.
---

# .NET Backend Standards (Jobfinder)

## Overview

Jobfinder's backend follows one strict pipeline:

```
Endpoint → Handler → Service
```

Never skip a layer, never mix concerns across layers. This skill is the guide to write
that shape and the checklist to verify it before you call the work done.

> **Local reference wins.** The project's authoritative rules live in
> [`src/backend/rules/`](../../../src/backend/rules/); jobfinder-specific exceptions are
> enumerated in [`CLAUDE.md`](../../../CLAUDE.md) → *"Backend rules: adopted vs. exceptions"*.
> This skill is the portable navigator — when it disagrees with `src/backend/rules/` or
> CLAUDE.md, those win. The full generic rule set travels in
> [`reference/`](reference/README.md).

## Jobfinder deviations from the generic rules (read first)

Jobfinder is a **local, single-user, file-based tool**. It deliberately opts out of parts
of the layered-SaaS rule set. **Do not "fix" these back:**

| Generic rule | Jobfinder reality |
|---|---|
| EF Core + SQL Server + migrations | JSON files under `data/<email>/`; SQLite only for Hangfire (`data/<email>/hangfire.db`). |
| `DbContext` at the service layer | Services take file-backed stores (`JobSearchStore`, `HistoryReader`, …). "Data access" means JSON on disk. |
| GUID primary keys | String run-ids (== history run id) for `JobSearch`; other domain records are immutable records with natural keys. |
| Permission-based auth (`.RequirePermission*`) | **No auth.** Never add `.RequirePermission*(...)`, `[Authorize]`, or JWT wiring. `/api/system/shutdown` is host-only; that's the only privileged endpoint. |
| JWT / identity / user principal | None. The "user" is `data/<email>/` resolved via `git config user.email` → `JOBFINDER_USER` env → error. |
| Hangfire dashboard secured with auth filter | Local-only, unsecured **by design**. |
| Hangfire retry attempts = 3 | The search job uses `[AutomaticRetry(Attempts = 1)]`. A full re-run is expensive and per-provider failures are already handled inside the pipeline. |
| Integration tests against a real database | xUnit + FluentAssertions against file-backed stores in a scratch data dir. `AddJobmatchApi(enableBackgroundJobs: false)` in the "Testing" environment. |
| `ConflictException` / `ForbiddenException` / `UnauthorizedException` | Not used. Typed exceptions in play are `ConfigException`, `InvalidRequestException`, `NotFoundException`. |

The `reference/security/auth.md` and `reference/data-access/entity-framework.md` /
`entity-ids.md` files have been **removed** from this skill because they do not apply. Do
not reintroduce.

## When to use

- **Before writing** any backend endpoint, handler, service, adapter, DI wiring, or test.
- **Before claiming done** — committing, opening a PR, or marking a task complete after touching backend code.

## What goes where

| Concern | Endpoint | Handler | Service |
|---------|---------|---------|---------|
| Routes, OpenAPI metadata | ✅ | | |
| Operation logging, exception → `IResult` translation | | ✅ | |
| Business logic, file-based data access, ranking, adapter orchestration | | | ✅ |

- **Endpoints** register routes only — no data-access types injected. Route constants live
  in `src/backend/Jobmatch.Api/Endpoints/Routes.cs`.
- **Handlers** inherit `HandlerBase` and wrap every public method body in `ExecuteAsync(...)`.
  That wrapper provides automatic logging and exception → HTTP mapping.
- **Services** own file I/O (via stores), adapter orchestration, ranking, and validation.
  They return DTOs / immutable records and throw typed exceptions — never return `IResult`.

## Hard limits

| Limit | Value |
|-------|-------|
| Any file | ≤ 300 lines |
| Method | ≤ 50 lines |
| Handler ctor params | ≤ 5 |
| Service ctor params | ≤ 7 |

Exceeding a limit is a refactoring trigger, not a judgment call. See
[`reference/conventions/maintainability.md`](reference/conventions/maintainability.md)
and CLAUDE.md → *"Code conventions"*.

## Exception → HTTP mapping (automatic via `HandlerBase.ExecuteAsync`)

| Throw | Get |
|-------|-----|
| `NotFoundException` | 404 |
| `InvalidRequestException` | 400 |
| `ConfigException` | 400 (invalid user configuration on disk) |
| `InternalDataInvalidException` | 500 |

Throw the right type from the service. Do not catch and translate manually — the base
wrapper does it. **Adapters throw on failure**; the `SearchService` orchestrator wraps
each adapter in try/catch, logs a structured warning, and continues. That pipeline-level
degradation is intentional — do not push it up into handlers. Full convention in
[`reference/security/error-handling.md`](reference/security/error-handling.md).

## Key conventions

- Collection expressions `[]`, not `new List<T>()`.
- `.Count > 0` on materialized collections, not `.Any()`.
- Immutable records for domain values; enums for domain values, not magic strings.
- Primary constructors for dependency injection.
- Nullable reference types on; warnings as errors.
- **No auth** — never add `.RequirePermission*(...)`, `[Authorize]`, JWT, or resource-scoping filters.
- **No EF Core, no `DbContext`, no migrations** — state is JSON files under `data/<email>/`
  reached through stores.
- Tests: xUnit + FluentAssertions, mirror the source tree, same size limits, no live
  network in CI.

## Per-user data (jobfinder-specific)

Every operation that reads or writes user state must resolve through `data/<email>/`. The
active email comes from `git config user.email` → `JOBFINDER_USER` env → clear error.
Never write user state outside `data/<email>/`. Never commit anything from `data/`.
Committed `src/backend/config/*.example.*` files are templates copied on first use;
`src/backend/config/ranking.yml` is the default and `data/<email>/ranking.yml` overrides.

## Read this before writing X

| Work type | Read first |
|-----------|-----------|
| New endpoint | [`reference/api/api-endpoints.md`](reference/api/api-endpoints.md) + [`reference/api/handlers.md`](reference/api/handlers.md) — **ignore any `.RequirePermission*` guidance** |
| New service method | [`reference/api/services.md`](reference/api/services.md) — swap `DbContext` for a file-backed store |
| Route / URI naming | [`reference/api/api-naming-conventions.md`](reference/api/api-naming-conventions.md) |
| OpenAPI wiring | [`reference/api/openapi.md`](reference/api/openapi.md) |
| New module / DI registration | [`reference/infrastructure/dependency-injection.md`](reference/infrastructure/dependency-injection.md) |
| Logging / config | [`reference/infrastructure/logging.md`](reference/infrastructure/logging.md), [`configuration.md`](reference/infrastructure/configuration.md) |
| Background jobs | [`reference/infrastructure/background-jobs.md`](reference/infrastructure/background-jobs.md) — **but** storage is SQLite (`data/<email>/hangfire.db`), dashboard is unsecured, and the search job uses `Attempts = 1` |
| Error handling / typed exceptions | [`reference/security/error-handling.md`](reference/security/error-handling.md) |
| New test | [`reference/testing/unit-tests.md`](reference/testing/unit-tests.md), [`integration-tests.md`](reference/testing/integration-tests.md), or [`e2e-tests.md`](reference/testing/e2e-tests.md) |
| Splitting an oversized file | [`reference/conventions/refactoring-strategy.md`](reference/conventions/refactoring-strategy.md) |

Architecture index: [`reference/README.md`](reference/README.md). Project-specific
conventions: [`src/backend/rules/`](../../../src/backend/rules/).

## Completion checklist

Run this against the backend `.cs` files you changed before claiming the work is done.
When a check flags a file, open it — do not skip.

| # | Check |
|---|-------|
| 1 | No store or file-I/O type injected into any endpoint. |
| 2 | Every handler inherits `HandlerBase`; every public method body opens with `ExecuteAsync(...)`. |
| 3 | Services throw typed exceptions (`ConfigException` / `InvalidRequestException` / `NotFoundException`); they never return `IResult`, `Result<>`, or `(bool, ...)` tuples. |
| 4 | Collection expressions `[]`, not `new List<T>()`. |
| 5 | `.Count > 0`, not `.Any()`, on materialized collections. |
| 6 | New endpoints declare `.Produces<T>(...)` for every status code. **No `.RequirePermission*(...)` — jobfinder has no auth.** |
| 7 | Each endpoint is its own `private static Map{Operation}(group)` method; `Register()` only builds the group and calls every `Map` method — no inline lambdas, no logic in `Register()`. |
| 8 | New behavior has a matching test (unit or integration). |
| 9 | File-size limit respected: ≤ 300 lines / method ≤ 50 lines. |
| 10 | Every user-state read/write resolves through `data/<email>/` (via the per-user context provider), never a hardcoded path. |
| 11 | No new EF Core / `DbContext` / migration / GUID-key code. If you feel you need one, re-read CLAUDE.md → *"Backend rules: adopted vs. exceptions"* first. |

## Red flags — these are not exceptions

| Excuse | Reality |
|--------|---------|
| "I'll add the test later" | Backend behavior without a test fails the rule. Add it now. |
| "The handler is small enough that the pattern doesn't matter" | The pattern is the shape of the codebase. Match it. |
| "The 300-line limit is a guideline" | It is a limit. Refactor before exceeding. |
| "Catching the exception here is cleaner" | `HandlerBase.ExecuteAsync` translates exceptions. Throw the typed exception, do not catch. |
| "Just one `new List<T>()` is fine" | Collection expressions are the convention — every one matters for consistency. |
| "Injecting the store into the handler saves a layer" | The service owns data access. The layer is not optional. |
| "This endpoint should require a permission" | Jobfinder has no auth. Add `.RequirePermission*(...)` only after a product decision reverses that. |
| "Let's persist this in a small SQLite table instead of JSON" | State is files under `data/<email>/`. SQLite is used only for Hangfire (`hangfire.db`). Adding a second store needs product approval. |
| "Bumping Hangfire retries back to 3 is safer" | The search job is `Attempts = 1` on purpose — a full re-run is expensive, per-provider failures degrade gracefully inside the pipeline. Don't change it. |

**Violating the letter of these rules is violating the spirit of them.** If a check is
genuinely ambiguous, read the relevant [`reference/`](reference/README.md) file, or
[`src/backend/rules/`](../../../src/backend/rules/), or CLAUDE.md — those are the source
of truth.
