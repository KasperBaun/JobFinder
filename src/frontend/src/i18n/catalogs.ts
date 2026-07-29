import { activeLocale } from './active'
import { da } from './da'
import { en } from './en'
import type { Messages } from './en'
import type { Locale } from './locale'

export const CATALOGS: Record<Locale, Messages> = { en, da }

/** For the non-component consumers that read the catalog during render — see active.ts. */
export function activeMessages(): Messages {
  return CATALOGS[activeLocale()]
}
