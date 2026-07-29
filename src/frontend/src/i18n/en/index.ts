import { common } from './common'
import { home } from './home'
import { nav } from './nav'
import { providers } from './providers'
import { search } from './search'
import { settings } from './settings'
import { setup } from './setup'
import { sources } from './sources'
import { skillset } from './skillset'

export const en = {
  common,
  home,
  nav,
  providers,
  search,
  settings,
  setup,
  skillset,
  sources,
}

/**
 * The shape every locale must satisfy. `da/index.ts` annotates itself with this, so a missing key,
 * an extra key, or a mismatched interpolation signature is a build error rather than a runtime
 * fallback — which is what `tsc -b` (run by the release publish) enforces in CI.
 */
export type Messages = typeof en
export type Namespace = keyof Messages
