import { useContext } from 'react'
import { I18nContext } from './context'
import type { Messages, Namespace } from './en'
import type { Locale } from './locale'

/**
 * Returns the namespace object itself, not a `t('key')` lookup — so `t.title` is a plain property
 * access with autocomplete, go-to-definition and rename-symbol, and a missing key fails the build
 * instead of rendering its own name.
 */
export function useT<N extends Namespace>(namespace: N): Messages[N] {
  return useContext(I18nContext).messages[namespace]
}

export function useLocale(): { locale: Locale; setLocale: (locale: Locale) => void } {
  const { locale, setLocale } = useContext(I18nContext)
  return { locale, setLocale }
}
