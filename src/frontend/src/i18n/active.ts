import type { Locale } from './locale'

// Mirror of the React locale state for the few consumers that are called during render but are not
// components — utils/time.ts (23 call sites) and the catalog entries that format numbers. Keeping
// this here, in a module with no other imports, is what stops the catalog ↔ formatter import cycle.
// I18nProvider is the only writer.
let active: Locale = 'en'

export function setActiveLocale(locale: Locale): void {
  active = locale
}

export function activeLocale(): Locale {
  return active
}
