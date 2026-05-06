// `npm run dev` — full-stack hot reload with no static ports.
//
// Orchestration:
//   1. Pick two free ports (api, vite) by briefly binding :0 and releasing.
//   2. Spawn `dotnet watch run` with JOBFINDER_PORT=<api> + browser-launch suppressed.
//   3. Spawn `vite dev` with JOBFINDER_VITE_PORT=<vite> + JOBFINDER_API_TARGET=<api>.
//   4. Probe both until ready, then open the browser at the vite URL once.
//
// React HMR is instant; C# changes hot-reload (or restart) the server, vite reconnects.
import { spawn } from 'node:child_process'
import { resolve } from 'node:path'
import { findFreePort, waitForPort, openBrowser, killTree } from './dev-utils.mjs'

const root = resolve(import.meta.dirname, '..')

const apiPort  = await findFreePort()
const vitePort = await findFreePort()
const apiTarget = `http://127.0.0.1:${apiPort}`

const env = {
  ...process.env,
  JOBFINDER_PORT: String(apiPort),
  JOBFINDER_NO_BROWSER: '1',
  JOBFINDER_API_TARGET: apiTarget,
  JOBFINDER_VITE_PORT: String(vitePort),
  DOTNET_WATCH_SUPPRESS_EMOJIS: '1',
}

console.log(`> dotnet watch run --project src/Jobmatch.Gui   [api  → :${apiPort}]`)
console.log(`> vite dev                                        [client → :${vitePort}, /api → ${apiTarget}]`)

const children = []
function start(label, cmd, args) {
  const child = spawn(cmd, args, { stdio: 'inherit', cwd: root, shell: true, env })
  children.push({ label, child })
  child.on('exit', (code, signal) => {
    if (!shuttingDown) {
      console.log(`\n[${label}] exited (${code ?? signal}); shutting down siblings.`)
      cleanup()
    }
  })
  return child
}

let shuttingDown = false
function cleanup() {
  if (shuttingDown) return
  shuttingDown = true
  for (const { child } of children) killTree(child.pid)
  setTimeout(() => process.exit(0), 200)
}
process.on('SIGINT',  cleanup)
process.on('SIGTERM', cleanup)
process.on('exit',    () => { for (const { child } of children) killTree(child.pid) })

start('server', 'dotnet', ['watch', '--project', 'src/Jobmatch.Gui', 'run'])
start('client', 'npm',    ['--prefix', 'src/Jobmatch.Gui/Client', 'run', 'dev'])

const [apiReady, viteReady] = await Promise.all([
  waitForPort(apiPort,  { timeoutMs: 90000 }),  // first dotnet build can be slow
  waitForPort(vitePort, { timeoutMs: 30000 }),
])

if (!apiReady)  console.warn(`\n  ! dotnet didn't bind :${apiPort} within 90s — check the log above.`)
if (!viteReady) console.warn(`\n  ! vite didn't bind :${vitePort} within 30s — check the log above.`)

if (apiReady && viteReady) {
  const url = `http://localhost:${vitePort}/`
  console.log(`\n  jobfinder dev → ${url}\n`)
  openBrowser(url)
}

// Hold the event loop open until a child exits or the user hits Ctrl+C.
// Without this the orchestrator would return after printing the ready banner
// and node would exit, killing its children with it.
await new Promise(() => {})
