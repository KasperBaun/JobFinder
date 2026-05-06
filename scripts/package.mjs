// Builds a NuGet tool package at ./pkg/Jobfinder.<version>.nupkg
import { spawnSync } from 'node:child_process'
import { rmSync, mkdirSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')
const pkgDir = resolve(root, 'pkg')

mkdirSync(pkgDir, { recursive: true })

const args = [
  'pack',
  'src/Jobmatch.Gui/Jobmatch.Gui.csproj',
  '-c', 'Release',
  '-p:BuildGui=true',
  `-o`, pkgDir,
  '--nologo',
]

console.log(`> dotnet ${args.join(' ')}`)
const result = spawnSync('dotnet', args, { stdio: 'inherit', cwd: root, shell: true })
process.exit(result.status ?? 1)
