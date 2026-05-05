# MWT tool — GUI / Server architecture analysis

Source: `C:\dev\privat\mwt\src\tool`. Scope: how the tool exposes the same pipeline through CLI, TUI (deprecated), and GUI, and the patterns we want to copy into `jobfinder`.

---

## 1. Top-level shape

```
src/tool/
├── Program.cs              mode router
├── src/
│   ├── CLI/                System.CommandLine commands, one per file
│   ├── TUI/                Spectre.Console wizard (deprecated)
│   ├── GUI/                Kestrel server + React SPA
│   │   ├── GuiApp.cs       entry point
│   │   ├── Server/         endpoints, handlers, DTOs, routes, SSE helper
│   │   └── Client/         Vite + React 19 SPA
│   └── Shared/             AppContext, Pipeline, Services, Store, Validation
└── documentation/
```

`Program.cs` is a mode router with no other logic:

```csharp
if (args is ["tui"])      { await TuiApp.Run(); return 0; }
if (args.Length == 0)     { await GuiApp.Run(); return 0; }
// otherwise → System.CommandLine root command tree
```

**Takeaway** — one binary, three personalities, one Shared library underneath.

---

## 2. The Shared backbone

| Surface | Purpose |
|---|---|
| `Shared.AppContext` | Singleton DI'd into the GUI: working dir, manifest, tool version |
| `Shared/Pipeline/ScaffoldContext` | Immutable value object the pipeline consumes; one constructor (`Create`) |
| `Shared/Pipeline/ScaffoldPipelineFactory.CreateDefaultSteps()` | The 8-step scaffold pipeline as `IStep[]` |
| `Shared/Services/*` | BrandService, DeploymentService, DevToolsService, ProjectStateService, TemplateEngine — pure logic |
| `Shared/Validation/*` | ProjectNameValidator, HexColorValidator — reused across all three entry points |
| `Shared/Store/*` | Optional Redux-style `Store<ProjectState>` + `ProjectReducer` (CLI + TUI use it; GUI doesn't) |

**The contract** — every entry point ends up calling `ScaffoldContext.Create(...)` and `ScaffoldPipelineFactory.CreateDefaultSteps()`. Adding a new pipeline input means touching `ScaffoldContext.Create` once + the entry-point boundary that needs to expose it.

**Store adoption is partial and that's deliberate.** GUI bypasses the store because the HTTP request body already serialises the full configuration in one shot — re-dispatching server-side adds indirection with no benefit. That tells us: the store is for stateful navigation (TUI wizards), not stateless request/response (GUI handlers).

---

## 3. GUI server (`src/tool/src/GUI/Server/`)

```
Server/
├── GuiServer.cs            Kestrel host + middleware + SPA fallback
├── Routes.cs               centralised route constants (nested classes per resource)
├── GuiLog.cs               console output formatter
├── SseHelper.cs            server-sent events helper
├── Endpoints/              one file per resource, MapXxx static methods
│   ├── WizardEndpoints.cs
│   ├── ProjectEndpoints.cs
│   ├── BrandEndpoints.cs
│   ├── DevToolsEndpoints.cs
│   ├── DocsEndpoints.cs
│   ├── ValidationEndpoints.cs
│   └── FilesystemEndpoints.cs
├── Handlers/               actual logic — pure static methods
│   ├── WizardHandler.cs
│   ├── ProjectHandler.cs
│   ├── BrandHandler.cs
│   └── …
└── Models/                 DTOs (request/response records)
    ├── ScaffoldRequest.cs
    ├── BrandDto.cs
    ├── StepEvent.cs        SSE payload
    └── …
```

### 3.1 `GuiServer.RunAsync`

1. **Find an ephemeral port** — `TcpListener(IPAddress.Loopback, 0)` then `Stop()`.
2. **Build with `WebApplication.CreateSlimBuilder`** — minimal, AOT-friendly. ApplicationName set explicitly so the assembly resolves.
3. **Suppress all hosting logs** (`builder.Logging.ClearProviders()` + level filter) so the user's terminal stays clean.
4. **Inject `AppContext` and `CancellationTokenSource` as singletons.**
5. **Global exception middleware** — `CreateSlimBuilder` skips it by default; without it, an unhandled throw silently drops the TCP connection and the browser sees "Failed to fetch". The middleware writes a JSON `{ error: ... }` 500.
6. **Static files from `{BaseDirectory}/gui/`** — the React build is copied there by MSBuild. HTML is served `Cache-Control: no-store`.
7. **Endpoint registration** — each endpoint group is a static `Map(WebApplication app)` method on its endpoint class.
8. **`/api/ping`** — heartbeat. Client polls, detects disconnect, shows overlay.
9. **`/api/shutdown`** — POST. Cancels the host token after 300ms so the response flushes first.
10. **SPA fallback** — every unmatched route returns `index.html` so React Router works after a hard refresh.
11. **Ctrl+C handler** — `e.Cancel = true` to stop the OS killing the process, then `cts.CancelAfter(500ms)` for graceful shutdown.
12. **Browser launch** — best-effort, swallows failures (`cmd /c start` on Windows, `open` on macOS, `xdg-open` on Linux).

### 3.2 Endpoint pattern

Each endpoint file is two lines per route — map the route, call a handler:

```csharp
public static class WizardEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet (Routes.Wizard.Options,  (MwtAppContext ctx) =>
            WizardHandler.GetOptions(ctx));

        app.MapPost(Routes.Wizard.Scaffold, async (ScaffoldRequest req, MwtAppContext ctx, HttpContext http) =>
            await WizardHandler.Scaffold(req, ctx, http));
    }
}
```

This keeps the endpoint file flat and mechanical. All real work lives in handlers, which are static, take their inputs as parameters (no fields), and never throw — they write SSE error events or return `Results.Problem`.

### 3.3 Routes.cs — single source of truth

```csharp
public static class Routes
{
    public static class Wizard      { public const string Options = "/api/options"; … }
    public static class Project     { public const string Info    = "/api/project"; … }
    public static class System      { public const string Ping    = "/api/ping"; … }
    …
}
```

Every endpoint, every fetch on the client side, refers to the same constant family. A typo can't go unnoticed because the C# side won't compile.

### 3.4 Long-running operations — Server-Sent Events

The scaffold pipeline emits `StepEvent` records (`type`, `description`, `step`, `total`, `error?`) over `text/event-stream`. The handler is an `IAsyncEnumerable` that yields one event per step:

```csharp
yield return new StepEvent("start",        "", -1, total, null);
yield return new StepEvent("step_running", step.Description, i, total, null);
yield return new StepEvent("step_done",    step.Description, i, total, null);
yield return new StepEvent("complete",     "", total, total, null);
```

`SseHelper` sets headers and `await context.Response.WriteAsync($"data: {json}\n\n")` per event. Yielding `await Task.Yield()` between events flushes them to the client before slow synchronous work runs. Errors during a step yield `step_failed` and bail; soft failures (e.g. migration step) yield `migration_failed` but allow `complete`.

### 3.5 Validation at the boundary

Handlers run validators (`ProjectNameValidator`, `HexColorValidator`) on the deserialised request *before* touching the pipeline. Errors collect into a list, get sent as a single `error` event, and the handler returns. The same validators are reused by CLI and TUI — that's the cross-cutting bit.

---

## 4. GUI client (`src/tool/src/GUI/Client/`)

```
Client/
├── package.json            React 19, Vite 6, React Query 5, react-router-dom 7
├── vite.config.ts
├── index.html
├── src/
│   ├── main.tsx            createRoot + QueryClientProvider + BrowserRouter
│   ├── App.tsx             Routes + ServerDisconnectedOverlay
│   ├── api/
│   │   ├── client.ts       fetch wrappers, one per endpoint
│   │   └── types.ts        request/response TS types (mirror DTO records)
│   ├── pages/
│   │   ├── hero/           landing
│   │   ├── wizard/         multi-step form, calls scaffold endpoint
│   │   ├── project/        opened-project dashboard
│   │   ├── DocsPage.tsx
│   │   └── ClosedPage.tsx
│   ├── hooks/
│   │   ├── useServerConnection.ts   ping loop, sets connected/disconnected
│   │   ├── useScaffoldStream.ts     SSE consumer for scaffold endpoint
│   │   ├── useStepStream.ts         generic SSE step consumer
│   │   ├── useActionStream.ts
│   │   └── useValidateName.ts       debounced validation against API
│   ├── components/         ui/ + common/
│   └── css/                plain CSS files imported in main.tsx
└── dist/                   build output, MSBuild copies it next to the binary
```

**Client conventions:**
- `apiFetch<T>` is the single wrapper that throws on `!res.ok` with the response body for context.
- Every endpoint has one named export: `getOptions`, `validateName`, `saveBrand`, …
- Every TS type in `types.ts` has a one-to-one C# DTO (no derived shapes). Adding a field touches both.
- `useServerConnection` polls `/api/ping` and renders `ServerDisconnectedOverlay` when the host has gone. This catches the user closing the terminal mid-session.
- `shutdown()` POSTs `/api/shutdown` and the React app navigates to `/closed`, displaying a "you can close this tab" page.
- React Query is used for the GETs (`['project']`, `['options']`, …); plain fetch for one-shot POSTs.

---

## 5. State sharing across modes

| Mode | State mechanism | Context |
|---|---|---|
| CLI | `Store<ProjectState>` built in-command from parsed flags, passed to `ScaffoldContextFactory.FromProjectState` | One-shot — store dies with the process. |
| TUI | `AppState` owns a single `Store<ProjectState>`; screens dispatch actions | Persistent across screen navigation. |
| GUI server | **No store** — `ScaffoldRequest` arrives with everything, handler calls `ScaffoldContext.Create` directly | The HTTP body *is* the state transfer. |
| GUI client | Plain `useState` per page; no Redux mirror | Validated by the server boundary anyway. |

**Cross-cutting** — `ScaffoldContext.Create`, `ScaffoldPipelineFactory.CreateDefaultSteps`, validators, and `Shared/Services/*` are reused unchanged by all three.

The takeaway for jobfinder: **don't introduce a shared store unless we actually need stateful navigation.** Right now jobfinder's CLI is one-shot and the GUI will be one-shot per page → skip the store, share via `Shared/` library + DTO contracts.

---

## 6. Build wiring

The React app is built by MSBuild (a target in the `csproj` runs `npm run build` and copies `dist/` to `{Output}/gui/`). The C# binary serves whatever's at `{BaseDir}/gui/`. If the directory is missing, the fallback returns "GUI not built. Run: dotnet build -p:BuildGui=true."

**Implication for jobfinder:** add an MSBuild target on the GUI project that conditionally runs `npm install && npm run build` and copies into the output. Skip it on CI if you want fast CLI-only builds.

---

## 7. Patterns we will adopt in jobfinder

| Pattern | What we copy | What we adapt |
|---|---|---|
| `Program.cs` mode router | No-args → GUI; otherwise CLI | Skip TUI — jobfinder doesn't need it. |
| `Shared/` library | Move pipeline-relevant code (Ranker, Deduper, Adapters, Verification) into a single shared lib | Already organised correctly inside `src/Jobmatch/`. |
| GUI server layout | `Endpoints/` + `Handlers/` + `Models/` + `Routes.cs` + global exception + `/api/ping` + `/api/shutdown` + SPA fallback | One server, no template directory wrinkles. |
| SSE for long ops | `RunListings` streams per-portal fetch + dedupe + rank as `step_running` / `step_done` events | New — jobfinder currently runs synchronously. |
| Validation at boundary | Reuse `Skillset.FromParsed`, portal config validation in handler before pipeline | Already exists in current code. |
| React SPA scaffold | Vite + React 19 + React Query + react-router-dom | Same. |
| `useServerConnection` heartbeat + `ServerDisconnectedOverlay` | Lift verbatim | Same. |
| `apiFetch<T>` + per-endpoint named exports + DTO mirror in `types.ts` | Lift the convention | Same. |
| Per-resource endpoint files | `WizardEndpoints` → `SearchEndpoints`, plus `PortalEndpoints`, `SkillsetEndpoints`, `HistoryEndpoints` | Resources are different, pattern is identical. |
| Ephemeral port + auto-launch browser | Lift verbatim | Same. |
| MSBuild copies `dist/` to `{Output}/gui/` | Lift verbatim | Same. |

## 8. Patterns we will *not* adopt

- **`Store<ProjectState>` + reducer** — CLI + GUI are stateless per call; no multi-screen wizard state to share.
- **TUI mode** — explicitly out.
- **`.mwt/manifest.json` project marker** — jobfinder doesn't scaffold; the per-user data dir under `data/<email>/` is the equivalent surface.
- **Brand presets / colour system** — irrelevant for jobfinder.
- **Hangfire / Postgres / Redis** — those belong to scaffolded projects, not the tool itself.

---

## 9. Concrete jobfinder analogue

```
src/
├── Program.cs                            no args → GUI; otherwise CLI
├── Jobmatch/                             (existing) shared library
│   ├── Models/, Configuration/, Adapters/, Ranking/, Deduplication/,
│   │   Output/, Verification/
│   └── (no change to logic — only rewire entry points)
├── Jobmatch.Cli/                         (existing) System.CommandLine commands
└── Jobmatch.Gui/
    ├── GuiApp.cs                         entry point — copy from mwt
    └── Server/
        ├── GuiServer.cs                  Kestrel host — copy from mwt
        ├── Routes.cs                     /api/skillset, /api/portals, /api/search, /api/history
        ├── GuiLog.cs, SseHelper.cs       copy from mwt
        ├── Endpoints/
        │   ├── SkillsetEndpoints.cs      GET active, POST update
        │   ├── PortalEndpoints.cs        GET list, POST toggle, POST add/remove
        │   ├── SearchEndpoints.cs        POST run (SSE per-portal progress)
        │   ├── HistoryEndpoints.cs       GET runs, POST mark good/bad
        │   └── ValidationEndpoints.cs    name validation reused from Shared
        ├── Handlers/                     one per endpoint group
        └── Models/                       request/response DTOs
    └── Client/                           Vite + React 19, mirrors mwt's structure
        └── src/
            ├── pages/
            │   ├── HomePage.tsx          dashboard — provider count, last run, marked count
            │   ├── ProvidersPage.tsx     configured portals, enable/disable, add new
            │   ├── SkillsetPage.tsx      view & edit active skillset
            │   ├── SearchPage.tsx        run a search, watch SSE progress
            │   └── HistoryPage.tsx       past runs, marks, "good match" rate over time
            ├── api/, hooks/, components/, css/
```

The `Shared/` separation in mwt corresponds to the existing `Jobmatch/` library in jobfinder — no new project needed for that, just keep the boundary clean (CLI and GUI both reference `Jobmatch/`, never each other).
