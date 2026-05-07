// Removes the globally-installed Jobfinder tool. Always exits 0 (idempotent —
// fine to run when the tool isn't installed; the dotnet error is just info).
import { spawnSync } from 'node:child_process'

const args = ['tool', 'uninstall', '--global', 'Jobfinder']
console.log(`> dotnet ${args.join(' ')}`)
spawnSync('dotnet', args, { stdio: 'inherit' })
process.exit(0)
