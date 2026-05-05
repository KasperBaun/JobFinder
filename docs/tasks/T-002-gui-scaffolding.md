# T-002 — GUI scaffolding (Kestrel + React)

## Why

Lift the GUI/Server pattern documented in [`docs/mwt-tool-analysis.md`](../mwt-tool-analysis.md) so the CLI's capabilities are reachable from a desktop-style app. Depends on T-001 — the GUI must read per-user state.

## Outcome

A new `src/Jobmatch.Gui/` project that:

- Exposes a `GuiApp.Run()` entry point invoked when `Program.cs` is called with no arguments.
- Hosts a Kestrel server on an ephemeral loopback port and auto-launches the default browser at it.
- Serves a Vite + React 19 SPA bundled into the binary's output under `gui/`.
- Mirrors `mwt`'s server layout: `Server/Endpoints/`, `Server/Handlers/`, `Server/Models/`, `Routes.cs`, `GuiServer.cs`, `GuiLog.cs`, `SseHelper.cs`.
- Includes `/api/ping` heartbeat and `/api/shutdown` for graceful close.
- Includes the global exception middleware copy from mwt (slim builder skips it by default).

## Scope

- New project `src/Jobmatch.Gui/Jobmatch.Gui.csproj` (referenced by `Jobmatch.slnx`, references `Jobmatch/`).
- Mode router in `src/Program.cs` (move existing CLI bootstrap into a `Cli` namespace if needed): no args → GUI, otherwise CLI.
- `Server/` directory with the layout above.
- `Client/` directory: Vite + React 19 + React Query 5 + react-router-dom 7. Mirror mwt's `package.json` versions.
- MSBuild target on `Jobmatch.Gui.csproj` that runs `npm install && npm run build` and copies `Client/dist/` into `$(OutDir)/gui/`. Gated on a property so CI can skip.
- First page: a stub home page that calls `GET /api/whoami` and shows the active user's email + data dir. Enough to prove the wiring.

## Out of scope

- Feature pages — covered in T-003.
- Auth (single local user; no login).
- Long-running pipeline streams — wired in T-003 along with search.

## Acceptance

- `dotnet build src/Jobmatch.slnx` succeeds with the GUI build target enabled.
- `dotnet run --project src/Jobmatch.Gui` (or `src/Jobmatch.Cli` with no args) launches a browser, the home page renders, and the user's email/data dir display.
- Closing the browser tab and POSTing `/api/shutdown` cleanly exits the host.
- `Ctrl+C` exits cleanly.

## Requirements covered

R-070, R-071, R-072, R-073.
