// Publishes the self-contained win-x64 .NET backend (React GUI bundled) to publish/win-x64/.
// Shared by the Electron desktop packaging (package:desktop) and the legacy Inno installer
// (package-installer.mjs). Set VERSION to override (default 0.1.0-local).
import { spawnSync } from 'node:child_process'
import { rmSync } from 'node:fs'
import { resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

export function publishBackend({ version = process.env.VERSION ?? '0.1.0-local', root } = {}) {
  const repoRoot = root ?? resolve(import.meta.dirname, '..', '..')
  const publishDir = resolve(repoRoot, 'publish', 'win-x64')
  const asmVersion = version.replace(/-.*$/, '') // dotnet AssemblyVersion wants pure numeric

  rmSync(publishDir, { recursive: true, force: true })
  console.log(`> publishing backend (win-x64, self-contained, GUI) → ${publishDir}`)
  const res = spawnSync(
    'dotnet',
    [
      'publish', 'src/infrastructure/Jobmatch.Host/Jobmatch.Host.csproj',
      '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
      '-p:BuildGui=true', `-p:Version=${asmVersion}`, `-p:InformationalVersion=${version}`,
      '-o', publishDir, '--nologo',
    ],
    { stdio: 'inherit', cwd: repoRoot },
  )
  if (res.status !== 0) process.exit(res.status ?? 1)
  return publishDir
}

// Run directly: `node src/scripts/publish-backend.mjs`
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  publishBackend()
}
