// Publishes the self-contained .NET backend (React GUI bundled) to publish/<rid>/.
// Used by the desktop packaging (npm run package:win / package:linux → electron-builder, which
// bundles this as extraResources) and by CI. The RID defaults to win-x64; pass another as the first
// CLI arg (e.g. `node src/scripts/publish-backend.mjs linux-x64`). Set VERSION to override version.
import { spawnSync } from 'node:child_process'
import { rmSync } from 'node:fs'
import { resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

export function publishBackend({ version = process.env.VERSION ?? '0.1.0-local', root, rid = 'win-x64' } = {}) {
  const repoRoot = root ?? resolve(import.meta.dirname, '..', '..')
  const publishDir = resolve(repoRoot, 'publish', rid)
  const asmVersion = version.replace(/-.*$/, '') // dotnet AssemblyVersion wants pure numeric

  rmSync(publishDir, { recursive: true, force: true })
  console.log(`> publishing backend (${rid}, self-contained, GUI) → ${publishDir}`)
  const res = spawnSync(
    'dotnet',
    [
      'publish', 'src/infrastructure/Jobmatch.Host/Jobmatch.Host.csproj',
      '-c', 'Release', '-r', rid, '--self-contained', 'true',
      '-p:BuildGui=true', `-p:Version=${asmVersion}`, `-p:InformationalVersion=${version}`,
      '-o', publishDir, '--nologo',
    ],
    { stdio: 'inherit', cwd: repoRoot },
  )
  if (res.status !== 0) process.exit(res.status ?? 1)
  return publishDir
}

// Run directly: `node src/scripts/publish-backend.mjs [rid]`
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  publishBackend({ rid: process.argv[2] ?? 'win-x64' })
}
