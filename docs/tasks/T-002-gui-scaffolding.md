# T-002 — GUI scaffolding (Kestrel + React 19)

## Why

Build the desktop-app entry point. The single binary launches a local server, opens the browser at it, and serves a React SPA. There is no CLI; this is the only entry point.

Architectural reference: [`docs/mwt-tool-analysis.md`](../mwt-tool-analysis.md) (GUI/Server section). Adopt the layout exactly; skip everything mwt-specific (brand presets, scaffold pipeline, project manifest).

## Outcome

A new `src/Jobmatch.Gui/` project that:

- Is the published assembly. Running it on a machine with no arguments starts a Kestrel server on an ephemeral loopback port and auto-launches the default browser there.
- Serves a Vite + React 19 SPA bundled into the binary's output under `gui/`.
- Mirrors mwt's server layout: `Server/Endpoints/`, `Server/Handlers/`, `Server/Models/`, `Routes.cs`, `GuiServer.cs`, `GuiLog.cs`, `SseHelper.cs`.
- Includes `/api/ping` heartbeat and `/api/shutdown` for graceful close.
- Includes the global exception middleware copy from mwt (slim builder skips it by default).
- Resolves the active user (`data/<email>/`) on startup and exposes it via `/api/whoami`.

## Scope

### .NET side

- New project `src/Jobmatch.Gui/Jobmatch.Gui.csproj` — `<OutputType>Exe</OutputType>`, .NET 10, references `..\Jobmatch\Jobmatch.csproj`, uses `Microsoft.AspNetCore.App` framework reference.
- `src/Jobmatch.Gui/Program.cs` — calls `GuiApp.Run()`. No mode router (no second mode).
- `src/Jobmatch.Gui/GuiApp.cs` — public static `Task Run()`.
- `src/Jobmatch.Gui/Server/GuiServer.cs` — Kestrel host, port discovery, logging suppression, exception middleware, static files from `{BaseDir}/gui/`, endpoint registration, ping, shutdown, SPA fallback, Ctrl+C / ProcessExit, browser auto-launch.
- `src/Jobmatch.Gui/Server/Routes.cs` — centralised route constants in nested static classes. Initial set: `System` (ping, shutdown), `Whoami`.
- `src/Jobmatch.Gui/Server/GuiLog.cs`, `SseHelper.cs` — copy mwt's helpers verbatim (no logic-specific bits).
- `src/Jobmatch.Gui/Server/Endpoints/WhoamiEndpoints.cs` — single `GET /api/whoami`.
- `src/Jobmatch.Gui/Server/Handlers/WhoamiHandler.cs` — resolves `UserContext`, returns `{ email, dataDir, toolVersion }`.
- `src/Jobmatch.Gui/Server/Models/WhoamiResponse.cs` — DTO record.
- New `src/Jobmatch/UserContext.cs` (in the library so any future caller can use it) — typed paths under `data/<email>/`, plus a static `Resolve(string? emailOverride = null, string? repoRoot = null)` returning a populated instance. Email resolution: explicit override → env `JOBFINDER_USER` → `git config user.email` → `ConfigException` with a clear message. First-run seeding: if `data/<email>/` doesn't exist, create it and copy `{BaseDir}/config/skillset.example.md` → `skillset.md` and `portals.example.yml` → `portals.yml`. `Jobmatch.csproj` copies `..\config\*.example.*` and `ranking.yml` to `$(OutDir)/config/` as content (`CopyToOutputDirectory=PreserveNewest`).

### React side

- `src/Jobmatch.Gui/Client/` with Vite + React 19 + react-router-dom 7 + @tanstack/react-query 5.
- `package.json`, `vite.config.ts`, `tsconfig.{json,app.json,node.json}`, `index.html`.
- `src/main.tsx` — createRoot + StrictMode + QueryClientProvider + BrowserRouter.
- `src/App.tsx` — routes `/` → HomePage, plus a `ServerDisconnectedOverlay` that shows when the heartbeat fails.
- `src/api/client.ts`, `src/api/types.ts` — `apiFetch<T>` wrapper, `getWhoami()`, `shutdown()`, plus typed responses mirroring the C# DTOs.
- `src/hooks/useServerConnection.ts` — polls `/api/ping` every 5s, returns `'connected' | 'disconnected'`.
- `src/components/ServerDisconnectedOverlay.tsx` — full-screen modal when disconnected.
- `src/pages/HomePage.tsx` — renders email, data dir, tool version from `/api/whoami` via React Query. Enough to prove wiring; feature pages are T-003.
- Plain CSS files imported in `main.tsx`. No Tailwind, no CSS-in-JS.
- `.gitignore` for `node_modules/` and `dist/`.

### Build wiring

- MSBuild target `BuildGuiClient` on `Jobmatch.Gui.csproj` runs `npm install && npm run build` and copies `Client/dist/*` into `$(OutDir)/gui/`. Gated on a `BuildGui` property (default `true` for Release, `false` for Debug — choose what doesn't slow normal dev builds; print a "set -p:BuildGui=true to bundle the SPA" hint when skipped).
- Update `src/Jobmatch.slnx` to include the new project.

## Out of scope

- Feature pages (Providers / Skillset / Search / History / Marks) — that's T-003.
- Multi-user account switching at runtime.
- Auth — single local user.
- Long-running search SSE stream — wired in T-003 alongside the search page.

## Acceptance

- `dotnet build src/Jobmatch.slnx` clean.
- `dotnet test src/Jobmatch.slnx` green (existing 86 tests still pass; UserContext gets unit tests for path resolution and first-run seeding).
- `dotnet run --project src/Jobmatch.Gui` (with the SPA built) launches the browser; HomePage shows the user's email + data dir.
- POST `/api/shutdown` exits the host cleanly within ~300 ms.
- Closing the terminal (Ctrl+C) exits cleanly without orphaned processes.

## Requirements covered

R-001, R-002, R-003, R-070, R-071, R-072, R-073.
