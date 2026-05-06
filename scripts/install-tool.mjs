// Installs the local Jobfinder NuGet package as a global dotnet tool.
// Bypasses the NuGet cache so a freshly-built package always wins.
import { spawnSync } from 'node:child_process'
import { resolve } from 'node:path'
import { existsSync, readdirSync } from 'node:fs'

const root = resolve(import.meta.dirname, '..')
const pkgDir = resolve(root, 'pkg')

if (!existsSync(pkgDir) || readdirSync(pkgDir).filter(f => f.endsWith('.nupkg')).length === 0) {
  console.error(`No .nupkg found in ${pkgDir}. Run \`npm run package\` first.`)
  process.exit(1)
}

const args = [
  'tool', 'install',
  '--global',
  'Jobfinder',
  '--add-source', pkgDir,
  '--ignore-failed-sources',
  '--no-cache',
]

console.log(`> dotnet ${args.join(' ')}`)
const result = spawnSync('dotnet', args, { stdio: 'inherit', shell: true })
process.exit(result.status ?? 1)
