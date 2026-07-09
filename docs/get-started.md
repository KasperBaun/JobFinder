# Get started — build and run from source

Prerequisites: **.NET 10 SDK**, **Node.js 20+**.

## Run in dev mode

```bash
# clone, then from the repo root:
npm run dev
```

`npm run dev` starts the API and the web client together on free ports and opens the app in your
browser. React changes hot-reload instantly; C# changes reload the server.

## Install it as a global tool

Prefer it as an installed app? Package and install the self-contained desktop tool:

```bash
npm run package        # builds the SPA + bundles the dotnet tool
npm run install:tool   # installs `jobfinder` as a global .NET tool
jobfinder              # launch it from anywhere
```

## Build the Windows installer yourself

If you'd rather not use the [prebuilt installer from the releases page](../../../releases/tag/latest):

```bash
npm run package:win    # self-contained backend publish + electron-builder NSIS installer
                       # → src/desktop/release/jobfinder-setup-*.exe
```

Run the resulting `jobfinder-setup-*.exe` to install the desktop app. On first launch jobfinder asks
you to confirm where to store your data (it suggests a folder; you choose) — nothing is written until
you agree.

## Other scripts

- `npm run build` — release build
- `npm test` — backend suite
- `npm run test:client` — frontend
- `npm run test:e2e` — Playwright

## Optional: the local AI judge

jobfinder ranks with keywords out of the box. To enable the on-device LLM judge, download the
Gemma 3 4B GGUF (~2.3 GB) from the **Search** screen — it runs in-process via LlamaSharp, fully
offline. Nothing about your search ever leaves the machine.
