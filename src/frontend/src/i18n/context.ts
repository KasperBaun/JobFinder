import { createContext } from 'react'
import { CATALOGS } from './catalogs'
import type { Messages } from './en'
import type { Locale } from './locale'

export type I18nContextValue = {
  locale: Locale
  messages: Messages
  setLocale: (locale: Locale) => void
}

// Unlike SearchRunContext this has a real default rather than null: a component rendered without
// the provider (every existing unit test) gets the English catalog instead of throwing.
export const I18nContext = createContext<I18nContextValue>({
  locale: 'en',
  messages: CATALOGS.en,
  setLocale: () => {},
})
