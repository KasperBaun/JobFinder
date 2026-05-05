# T-001 — Per-user data path

## Why

Today the CLI reads `./config/` and writes `./data/` from the current working directory. The new contract (R-001..R-003) is that every user is identified by email and all of their state lives under `data/<email>/`.

## Outcome

- A new `Shared/UserContext` (or equivalent in `Jobmatch/`) resolves the active email and exposes typed paths.
- The CLI and (later) the GUI both go through it. No raw `Path.Combine(root, "config", …)` left in the code.
- The committed `src/config/` only holds examples + the default `ranking.yml`.

## Scope

- Add a single resolver: `UserContext.Resolve(emailOverride?) → { Email, RootDir, SkillsetPath, PortalsPath, RankingPath, ImportsDir, RawDir, OutputsDir, HistoryDir, MarksFile }`.
- Resolution order for email: `--user` flag → env var `JOBFINDER_USER` → `git config user.email` → prompt on first interactive run → fail in non-interactive runs with a clear error.
- Resolution order for `ranking.yml`: `data/<email>/ranking.yml` (override) → `src/config/ranking.yml` (default).
- First-run UX: if `data/<email>/` doesn't exist, create it and copy `src/config/skillset.example.md` and `src/config/portals.example.yml` (renamed) into it. Print where they landed.
- Replace every hard-coded path in `ListingsCommand`, `SkillsetCommand`, `VerifyCommand`, `ConfigVerifier`, and the integration tests.

## Out of scope

- Multi-user switching at runtime (no need yet — one user per process).
- Encryption of user data.
- Migrating *other* users' historic `data/` content — only the active user's files exist today.

## Acceptance

- `dotnet test` green; integration tests now exercise `data/<email>/` paths.
- `dotnet run --project src/Jobmatch.Cli -- listings --user me@example.com` reads from and writes to `data/me@example.com/`.
- Running with no `--user` and no git config in a fresh shell prints a clear, actionable error.

## Requirements covered

R-001, R-002, R-003.
