import { app } from 'electron'
import * as fs from 'node:fs'
import * as path from 'node:path'

const BACKEND_EXE = process.platform === 'win32' ? 'Jobmatch.Host.exe' : 'Jobmatch.Host'

// Where the self-contained .NET backend lives. Packaged: bundled under resources/backend
// (electron-builder extraResources). Dev: the repo's publish output — the same folder the
// installer ships — resolved relative to this app (src/desktop -> repo root -> publish/<rid>).
export function backendDir(): string {
  if (app.isPackaged) return path.join(process.resourcesPath, 'backend')
  const rid = process.platform === 'win32' ? 'win-x64' : 'linux-x64'
  return path.resolve(app.getAppPath(), '..', '..', 'publish', rid)
}

export function backendExePath(): string {
  const exe = path.join(backendDir(), BACKEND_EXE)
  if (!fs.existsSync(exe)) {
    const hint = app.isPackaged
      ? 'The install looks incomplete — reinstall Jobfinder.'
      : 'Run "npm run publish:backend" from the repo root first.'
    throw new Error(`Backend not found at:\n${exe}\n\n${hint}`)
  }
  return exe
}
