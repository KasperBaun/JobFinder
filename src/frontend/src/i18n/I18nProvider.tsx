import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { setActiveLocale } from './active'
import { CATALOGS } from './catalogs'
import { I18nContext } from './context'
import type { Locale } from './locale'
import { initialLocale, writeStoredLocale } from './storage'

type Props = {
  children: ReactNode
  /** Pins the locale — for tests, and for rendering a fixed language in isolation. */
  locale?: Locale
}

/**
 * Owns the interface language. Lives above the query client so it never depends on the server being
 * reachable; LanguageSync adopts the persisted server choice once /api/setup/status resolves.
 */
export function I18nProvider({ children, locale: pinned }: Props) {
  const [locale, setLocaleState] = useState<Locale>(() => pinned ?? initialLocale())

  // Mirrored during render rather than in an effect: utils/time.ts reads the active locale while
  // this same tree renders, so an effect would format the first paint with the previous language.
  // The write is idempotent, so StrictMode's double-invoke is harmless.
  setActiveLocale(locale)

  useEffect(() => {
    if (pinned) setLocaleState(pinned)
  }, [pinned])

  useEffect(() => {
    document.documentElement.lang = locale
  }, [locale])

  const setLocale = useCallback((next: Locale) => {
    writeStoredLocale(next)
    setLocaleState(next)
  }, [])

  const value = useMemo(
    () => ({ locale, messages: CATALOGS[locale], setLocale }),
    [locale, setLocale],
  )

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}
