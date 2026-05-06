// uninstall (idempotent) → package → install. The end-to-end "I changed code, give me the new tool" flow.
import { spawnSync } from 'node:child_process'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')

function run(args, { mustSucceed }) {
  console.log(`\n> npm run ${args}`)
  const result = spawnSync('npm', ['run', args], { stdio: 'inherit', cwd: root, shell: true })
  if (mustSucceed && result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

run('uninstall:tool', { mustSucceed: false })
run('package', { mustSucceed: true })
run('install:tool', { mustSucceed: true })

console.log('\nrefresh complete — `jobfinder` is up-to-date.')
