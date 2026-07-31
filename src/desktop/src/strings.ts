import { app } from 'electron'
import * as fs from 'node:fs'
import * as path from 'node:path'

type Locale = 'en' | 'da'

// The shell's own failure-path copy. These render before the SPA exists, so they cannot come from
// the frontend catalog — but leaving them English while the rest of the app is Danish would make
// the startup-error window the one screen that switches language.
const STRINGS = {
  en: {
    htmlLang: 'en',
    errorWindowTitle: 'Jobfinder — startup error',
    errorHeading: 'Jobfinder couldn’t start',
    errorLede: 'The background service failed to launch. Details:',
    quit: 'Quit',

    noFreePort: 'could not resolve a free port',
    launchFailed: (detail: string) => `Could not launch the backend:\n${detail}`,
    exitedDuringStartup: (log: string) => `The backend exited during startup.\n\n${log}`,
    startupTimedOut: (seconds: number, log: string) =>
      `The backend did not become ready within ${seconds}s.\n\n${log}`,

    installIncomplete: 'The install looks incomplete — reinstall Jobfinder.',
    publishBackendFirst: 'Run "npm run publish:backend" from the repo root first.',
    backendNotFound: (exe: string, hint: string) => `Backend not found at:\n${exe}\n\n${hint}`,

    pdfFilter: 'PDF',
    pdfDefaultName: 'job listing',
  },
  da: {
    htmlLang: 'da',
    errorWindowTitle: 'Jobfinder — fejl ved opstart',
    errorHeading: 'Jobfinder kunne ikke starte',
    errorLede: 'Baggrundstjenesten kunne ikke startes. Detaljer:',
    quit: 'Afslut',

    noFreePort: 'kunne ikke finde en ledig port',
    launchFailed: (detail: string) => `Baggrundstjenesten kunne ikke startes:\n${detail}`,
    exitedDuringStartup: (log: string) => `Baggrundstjenesten stoppede under opstart.\n\n${log}`,
    startupTimedOut: (seconds: number, log: string) =>
      `Baggrundstjenesten blev ikke klar inden for ${seconds} sekunder.\n\n${log}`,

    installIncomplete: 'Installationen ser ufuldstændig ud — geninstallér Jobfinder.',
    publishBackendFirst: 'Kør "npm run publish:backend" fra repo-roden først.',
    backendNotFound: (exe: string, hint: string) => `Baggrundstjenesten blev ikke fundet i:\n${exe}\n\n${hint}`,

    pdfFilter: 'PDF',
    pdfDefaultName: 'jobopslag',
  },
} satisfies Record<Locale, unknown>

function bootstrapPath(): string {
  const override = process.env.JOBFINDER_BOOTSTRAP
  if (override) return override
  return path.join(app.getPath('appData'), 'jobfinder', 'bootstrap.json')
}

// Reads the same bootstrap.json the backend writes, so the shell agrees with the SPA. Falls back to
// the OS locale, which is the best guess available before the user has ever completed setup.
function resolveLocale(): Locale {
  try {
    const raw = fs.readFileSync(bootstrapPath(), 'utf-8')
    const language = (JSON.parse(raw) as { language?: unknown }).language
    if (language === 'da' || language === 'en') return language
  } catch {
    // no bootstrap yet, or unreadable — fall through to the OS locale
  }
  try {
    return app.getLocale().toLowerCase().startsWith('da') ? 'da' : 'en'
  } catch {
    return 'en'
  }
}

let cached: (typeof STRINGS)[Locale] | null = null

export function t(): (typeof STRINGS)[Locale] {
  cached ??= STRINGS[resolveLocale()]
  return cached
}
