// Removes build outputs: pkg/, src/frontend/dist/, plus dotnet bin/obj.
import { spawnSync } from 'node:child_process'
import { rmSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')

const dirs = [
  'pkg',
  'src/frontend/dist',
]
for (const d of dirs) {
  const full = resolve(root, d)
  console.log(`rm -rf ${d}`)
  rmSync(full, { recursive: true, force: true })
}

console.log('> dotnet clean src/Jobmatch.slnx')
spawnSync('dotnet', ['clean', 'src/Jobmatch.slnx', '--nologo', '-v', 'minimal'], {
  stdio: 'inherit', cwd: root,
})
