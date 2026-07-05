// Tiny helpers shared by dev.mjs. Kept inline (no deps) so `npm run dev`
// has zero installation cost beyond what npm already pulled for vite/react.
import net from 'node:net'
import http from 'node:http'
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

/**
 * POST /api/system/shutdown to ask the .NET host to cancel itself. Critical on
 * Windows: if we just taskkill Jobmatch.Host, `dotnet watch` reads the abrupt
 * exit as a crash and immediately respawns the app — the new instance becomes
 * orphaned the moment we kill watch, leaving a zombie that holds the build
 * output's file handles. A clean exit (code 0) tells watch to stop instead.
 */
export function postShutdown(port, timeoutMs = 1500) {
  return new Promise((resolve) => {
    const req = http.request({
      host: '127.0.0.1', port, method: 'POST', path: '/api/system/shutdown', timeout: timeoutMs,
    }, (res) => { res.resume(); res.on('end', () => resolve(true)) })
    req.on('error',   () => resolve(false))
    req.on('timeout', () => { req.destroy(); resolve(false) })
    req.end()
  })
}

/**
 * Polls a port until nothing is listening on it (or the timeout elapses).
 * Used after `postShutdown` to confirm the .NET host has actually exited
 * before we taskkill its parent dotnet-watch — without this confirmation
 * we re-introduce the respawn race we were trying to avoid.
 */
export async function waitForPortFree(port, { timeoutMs = 5000, intervalMs = 150 } = {}) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const stillUp = await new Promise((resolve) => {
      const sock = net.createConnection({ port, host: '127.0.0.1' })
      sock.once('connect', () => { sock.end(); resolve(true) })
      sock.once('error',   () => { sock.destroy(); resolve(false) })
    })
    if (!stillUp) return true
    await new Promise((r) => setTimeout(r, intervalMs))
  }
  return false
}

/**
 * Final safety net on Windows: walk all descendants of `rootPid` via
 * Win32_Process and force-kill each. Catches orphans whose parent chain
 * was severed before we got to taskkill /T (e.g. a respawn that happened
 * mid-shutdown). No-op on POSIX. wmic is gone on recent Windows builds,
 * so we use Get-CimInstance — startup cost is paid only on shutdown.
 */
export function killDescendantsOnWindows(rootPid) {
  if (process.platform !== 'win32' || !rootPid) return
  const script = `
    $root = ${rootPid}
    $procs = Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId
    $byParent = @{}
    foreach ($p in $procs) {
      if (-not $byParent.ContainsKey([int]$p.ParentProcessId)) {
        $byParent[[int]$p.ParentProcessId] = New-Object System.Collections.ArrayList
      }
      [void]$byParent[[int]$p.ParentProcessId].Add([int]$p.ProcessId)
    }
    $visited = @{}
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($root)
    while ($queue.Count -gt 0) {
      $cur = $queue.Dequeue()
      if ($byParent.ContainsKey($cur)) {
        foreach ($child in $byParent[$cur]) {
          if (-not $visited.ContainsKey($child)) {
            $visited[$child] = $true
            $queue.Enqueue($child)
          }
        }
      }
    }
    foreach ($id in $visited.Keys) {
      Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
  `
  spawnSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', script], { stdio: 'ignore' })
}
