import { detectBrowserLocale, isLocale } from './locale'
import type { Locale } from './locale'

const KEY = 'jobfinder.lang'

// A boot hint only — bootstrap.json on the server stays authoritative. Reading it synchronously at
// provider init is what avoids a flash of English while /api/setup/status is in flight. Access is
// guarded: private-mode browsers and some sandboxes throw on localStorage.
export function readStoredLocale(): Locale | null {
  try {
    const raw = localStorage.getItem(KEY)
    return isLocale(raw) ? raw : null
  } catch {
    return null
  }
}

export function writeStoredLocale(locale: Locale): void {
  try {
    localStorage.setItem(KEY, locale)
  } catch {
    // no storage available — the server copy still carries the choice across sessions
  }
}

export function initialLocale(): Locale {
  return readStoredLocale() ?? detectBrowserLocale()
}
