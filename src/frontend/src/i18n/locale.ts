export type Locale = 'en' | 'da'

export const LOCALES: readonly Locale[] = ['en', 'da']

/** Shown in the language picker — each language named in itself, never translated. */
export const LOCALE_LABEL: Record<Locale, string> = {
  en: 'English',
  da: 'Dansk',
}

export function isLocale(value: unknown): value is Locale {
  return value === 'en' || value === 'da'
}

export function detectBrowserLocale(): Locale {
  if (typeof navigator === 'undefined') return 'en'
  const tags = [navigator.language, ...(navigator.languages ?? [])]
  return tags.some(tag => tag?.toLowerCase().startsWith('da')) ? 'da' : 'en'
}
