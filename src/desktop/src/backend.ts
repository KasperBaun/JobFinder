import { spawn, type ChildProcess } from 'node:child_process'
import * as net from 'node:net'
import * as path from 'node:path'
import { backendExePath } from './paths'

export interface BackendHandle {
  port: number
  child: ChildProcess
}

const STARTUP_TIMEOUT_MS = 30_000
const PING_INTERVAL_MS = 250
const SHUTDOWN_GRACE_MS = 3_000

const delay = (ms: number) => new Promise<void>((r) => setTimeout(r, ms))

// Ephemeral loopback port, mirroring the C# host's FindAvailablePort so the shell and backend agree.
function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const srv = net.createServer()
    srv.unref()
    srv.on('error', reject)
    srv.listen(0, '127.0.0.1', () => {
      const addr = srv.address()
      if (addr && typeof addr === 'object') {
        const { port } = addr
        srv.close(() => resolve(port))
      } else {
        srv.close(() => reject(new Error('could not resolve a free port')))
      }
    })
  })
}

async function ping(port: number): Promise<boolean> {
  try {
    const res = await fetch(`http://127.0.0.1:${port}/api/system/ping`, {
      signal: AbortSignal.timeout(2_000),
    })
    return res.ok
  } catch {
    return false
  }
}

// Launches the bundled .NET backend as a hidden child on a private loopback port, then waits for its
// heartbeat. Throws (with captured output) on spawn failure, early exit, or a startup timeout.
export async function startBackend(): Promise<BackendHandle> {
  const exe = backendExePath()
  const port = await findFreePort()

  const child = spawn(exe, [], {
    cwd: path.dirname(exe), // so {BaseDir}/gui and the native LLaMa libs resolve
    windowsHide: true, // no console window
    stdio: ['ignore', 'pipe', 'pipe'],
    env: {
      ...process.env,
      JOBFINDER_PORT: String(port),
      JOBFINDER_NO_BROWSER: '1',
    },
  })

  const tail: string[] = []
  const capture = (buf: Buffer) => {
    tail.push(buf.toString())
    while (tail.length > 50) tail.shift()
  }
  child.stdout?.on('data', capture)
  child.stderr?.on('data', capture)

  // These are mutated inside the child's event callbacks; read via helpers so TS control-flow
  // analysis doesn't narrow the closure-assigned locals to `never` at the checks below.
  let spawnError: Error | null = null
  let exited = false
  child.on('error', (err) => {
    spawnError = err
  })
  child.on('exit', () => {
    exited = true
  })
  const getSpawnError = (): Error | null => spawnError
  const hasExited = (): boolean => exited

  const deadline = Date.now() + STARTUP_TIMEOUT_MS
  while (Date.now() < deadline) {
    const err = getSpawnError()
    if (err) throw new Error(`Could not launch the backend:\n${err.message}`)
    if (hasExited()) throw new Error(`The backend exited during startup.\n\n${tail.join('')}`)
    if (await ping(port)) return { port, child }
    await delay(PING_INTERVAL_MS)
  }

  try {
    child.kill()
  } catch {
    // best effort
  }
  throw new Error(`The backend did not become ready within 30s.\n\n${tail.join('')}`)
}

// Graceful shutdown: ask the host to stop, wait briefly, then force-kill so nothing is orphaned.
export async function shutdownBackend(handle: BackendHandle): Promise<void> {
  const { port, child } = handle
  try {
    await fetch(`http://127.0.0.1:${port}/api/system/shutdown`, {
      method: 'POST',
      signal: AbortSignal.timeout(2_000),
    })
  } catch {
    // the force-kill below is the backstop
  }

  const deadline = Date.now() + SHUTDOWN_GRACE_MS
  while (Date.now() < deadline) {
    if (child.exitCode !== null) return
    await delay(150)
  }

  killChild(child)
}

export function killChild(child: ChildProcess): void {
  if (child.exitCode !== null) return
  try {
    child.kill()
  } catch {
    // fall through to taskkill
  }
  if (process.platform === 'win32' && child.pid) {
    try {
      spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { windowsHide: true })
    } catch {
      // nothing more we can do
    }
  }
}
