---
name: verify
description: Runtime-verify a jobfinder change end-to-end — boot the API against a scratch data dir, seed history runs, drive the SPA with Playwright.
---

# Verifying jobfinder changes at runtime

## Handle

The backend and frontend run independently; no real user data is touched if you
redirect the bootstrap:

```bash
# 1. Scratch env: bootstrap.json pointing at a scratch data dir
S=<scratch>; mkdir -p $S/data/verify@example.com/history
echo '{ "email": "verify@example.com", "dataDir": "'$S'/data/verify@example.com" }' > $S/bootstrap.json

# 2. API (respects JOBFINDER_BOOTSTRAP + JOBFINDER_PORT; build first, then --no-build)
JOBFINDER_BOOTSTRAP=$S/bootstrap.json JOBFINDER_PORT=58731 \
  dotnet run --project src/backend/Jobmatch.Api --no-build   # background

# 3. SPA via Vite proxy (env names from src/frontend/vite.config.ts)
cd src/frontend && JOBFINDER_VITE_PORT=58732 \
  JOBFINDER_API_TARGET=http://127.0.0.1:58731 npm run dev    # background
```

Readiness: `curl http://127.0.0.1:58731/api/system/ping`.

## Seeding state

- History runs: drop `<runId>.json` files (camelCase `RunDetail` shape — copy a
  real one or the sample in an existing test) into `$S/data/<email>/history/`.
  Run ids are timestamp strings (`20260706-090000-xxxxxx`); newest = highest.
  The API reads them cold — no restart needed.
- Marks: write via `POST /api/marks` / `POST /api/marks/status`, then inspect
  `$S/data/<email>/marks.json` directly for on-disk shape checks.

## Driving the GUI

Playwright chromium is already installed under `src/tests/playwright/node_modules`.
A standalone script can import it directly:

```js
import { chromium } from '<repo>/src/tests/playwright/node_modules/@playwright/test/index.mjs'
```

Point it at `http://127.0.0.1:58732`. Useful routes: `/history/<runId>`
(shortlist cards; `#tab=longlist` for the table), `/applications`, `/`.
Setup is already "configured" thanks to the bootstrap file, so no first-run wizard.

## Gotchas

- `dotnet run` without `--no-build` is slow; run `dotnet build src/Jobmatch.slnx` once first.
- Hangfire creates `hangfire.db` in the scratch data dir on API start — expected.
- A run detail's marks are only visible through `GET /api/history/<runId>` (no GET /api/marks).
