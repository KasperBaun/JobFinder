// Builds a Windows distribution locally, without GitHub:
//   1. self-contained win-x64 publish (GUI bundled)      -> publish/win-x64/
//   2. a portable zip you can copy to any Windows box     -> src/installer/Output/jobfinder-portable-<v>-win-x64.zip
//   3. the Inno Setup installer .exe, IF Inno Setup (ISCC) is available (native or via wine)
//
// Set VERSION to override the version (default 0.1.0-local). Point ISCC at ISCC.exe to force a path.
import { spawnSync } from 'node:child_process'
import { rmSync, mkdirSync, existsSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..', '..')
const publishDir = resolve(root, 'publish', 'win-x64')
const outDir = resolve(root, 'src', 'installer', 'Output')

const version = process.env.VERSION ?? '0.1.0-local'
const asmVersion = version.replace(/-.*$/, '') // dotnet AssemblyVersion wants pure numeric

function run(cmd, args, opts = {}) {
  console.log(`> ${cmd} ${args.join(' ')}`)
  return spawnSync(cmd, args, { stdio: 'inherit', cwd: root, ...opts })
}

function exists(cmd) {
  const probe = spawnSync(process.platform === 'win32' ? 'where' : 'which', [cmd], { stdio: 'ignore' })
  return probe.status === 0
}

// 1. Publish -------------------------------------------------------------
rmSync(publishDir, { recursive: true, force: true })
const publish = run('dotnet', [
  'publish', 'src/infrastructure/Jobmatch.Host/Jobmatch.Host.csproj',
  '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
  '-p:BuildGui=true', `-p:Version=${asmVersion}`, `-p:InformationalVersion=${version}`,
  '-o', publishDir, '--nologo',
])
if (publish.status !== 0) process.exit(publish.status ?? 1)

mkdirSync(outDir, { recursive: true })

// 2. Portable zip --------------------------------------------------------
const portableZip = resolve(outDir, `jobfinder-portable-${version}-win-x64.zip`)
rmSync(portableZip, { force: true })
let portableOk = false
if (process.platform === 'win32') {
  portableOk = run('powershell', ['-NoProfile', '-Command',
    `Compress-Archive -Path '${publishDir}\\*' -DestinationPath '${portableZip}' -Force`]).status === 0
} else if (exists('zip')) {
  portableOk = run('zip', ['-r', '-q', portableZip, '.'], { cwd: publishDir }).status === 0
}
if (!portableOk) {
  console.log('  (skipped portable zip — no zip tool found; the folder itself is the portable app)')
}

// 3. Installer via Inno Setup -------------------------------------------
function findIscc() {
  if (process.env.ISCC) return { cmd: process.env.ISCC, pre: [] }
  const localAppData = process.env.LOCALAPPDATA ?? ''
  const winPaths = [
    'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe',
    'C:\\Program Files\\Inno Setup 6\\ISCC.exe',
    // winget installs Inno Setup per-user by default (current-user scope)
    ...(localAppData ? [resolve(localAppData, 'Programs', 'Inno Setup 6', 'ISCC.exe')] : []),
  ]
  for (const p of winPaths) if (existsSync(p)) return { cmd: p, pre: [] }
  if (exists('ISCC.exe')) return { cmd: 'ISCC.exe', pre: [] }
  if (exists('iscc')) return { cmd: 'iscc', pre: [] }
  // Linux/mac: allow wine + an ISCC path from ISCC_EXE
  if (process.env.ISCC_EXE && exists('wine')) return { cmd: 'wine', pre: [process.env.ISCC_EXE] }
  return null
}

const iscc = findIscc()
let installerBuilt = false
if (iscc) {
  const res = run(iscc.cmd, [...iscc.pre, `/DAppVersion=${version}`, 'src/installer/jobfinder.iss'])
  installerBuilt = res.status === 0
} else {
  console.log('\n  Inno Setup (ISCC) not found — skipping the installer .exe.')
  console.log('  To build it locally, install Inno Setup 6 (https://jrsoftware.org/isdl.php)')
  console.log('  on Windows, or on Linux/mac run with wine: ISCC_EXE="/path/to/ISCC.exe" npm run package:win')
}

// Summary ----------------------------------------------------------------
console.log('\nDone.')
console.log(`  Portable app folder : ${publishDir}`)
if (portableOk) console.log(`  Portable zip        : ${portableZip}`)
if (installerBuilt) console.log(`  Installer           : ${resolve(outDir, `jobfinder-setup-${version}.exe`)}`)
console.log('\n  Copy the folder or zip to a Windows machine and run Jobmatch.Host.exe.')
