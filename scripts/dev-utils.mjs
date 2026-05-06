// Tiny helpers shared by dev.mjs. Kept inline (no deps) so `npm run dev`
// has zero installation cost beyond what npm already pulled for vite/react.
import net from 'node:net'
import { spawn, spawnSync } from 'node:child_process'

/**
 * Asks the OS for a free TCP port by binding `:0` and immediately releasing it.
 * Standard pattern used by get-port / portfinder / vite — there's a sub-ms race
 * window between close() and the child's bind(), but on a dev machine the kernel
 * doesn't reissue just-freed ports, so collisions are virtually never observed.
 */
export function findFreePort() {
  return new Promise((resolve, reject) => {
    const srv = net.createServer()
    srv.unref()
    srv.on('error', reject)
    srv.listen(0, '127.0.0.1', () => {
      const { port } = srv.address()
      srv.close((err) => (err ? reject(err) : resolve(port)))
    })
  })
}

/**
 * Polls a port on both IPv4 and IPv6 loopback until something is listening or
 * `timeoutMs` elapses. Vite's default `host: 'localhost'` binds to whichever
 * stack node's DNS resolves first — on modern Windows that's frequently `::1`,
 * so probing only `127.0.0.1` would falsely time out while vite is happily up.
 */
export async function waitForPort(port, { timeoutMs = 60000, intervalMs = 200 } = {}) {
  const hosts = ['127.0.0.1', '::1']
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    for (const host of hosts) {
      const ok = await new Promise((resolve) => {
        const sock = net.createConnection({ port, host })
        sock.once('connect', () => { sock.end(); resolve(true) })
        sock.once('error',   () => { sock.destroy(); resolve(false) })
      })
      if (ok) return true
    }
    await new Promise((r) => setTimeout(r, intervalMs))
  }
  return false
}

/**
 * Cross-platform "open this URL in the default browser." Best-effort —
 * we don't propagate failures because the URL is also printed to the terminal.
 */
export function openBrowser(url) {
  try {
    if (process.platform === 'win32') {
      spawn('cmd', ['/c', 'start', '""', url], { stdio: 'ignore', detached: true }).unref()
    } else if (process.platform === 'darwin') {
      spawn('open', [url], { stdio: 'ignore', detached: true }).unref()
    } else {
      spawn('xdg-open', [url], { stdio: 'ignore', detached: true }).unref()
    }
  } catch {
    // intentional — the URL is in the log; user can click manually
  }
}

/**
 * Kills a child and (on Windows) all its descendants. Without /T on Windows,
 * `npm` would survive after we kill it but its grandchild (vite/dotnet) would
 * be orphaned, leaving the dev port stuck.
 */
export function killTree(pid) {
  if (!pid) return
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/pid', String(pid), '/T', '/F'], { stdio: 'ignore' })
  } else {
    try { process.kill(pid, 'SIGTERM') } catch { /* already dead */ }
  }
}
