// uninstall (idempotent) → package → install. The end-to-end "I changed code, give me the new tool" flow.
import { spawnSync } from 'node:child_process'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')

// `npm` is a .cmd shim on Windows; Node refuses to spawn .cmd without shell:true
// (CVE-2024-27980). Combining shell:true with an args array triggers DEP0190.
// Workaround: shell:true + single command string. Safe here because `args` is a
// hardcoded script name from this file, never user input.
function run(args, { mustSucceed }) {
  console.log(`\n> npm run ${args}`)
  const result = spawnSync(`npm run ${args}`, { stdio: 'inherit', cwd: root, shell: true })
  if (mustSucceed && result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

run('uninstall:tool', { mustSucceed: false })
run('package', { mustSucceed: true })
run('install:tool', { mustSucceed: true })

console.log('\nrefresh complete — `jobfinder` is up-to-date.')
