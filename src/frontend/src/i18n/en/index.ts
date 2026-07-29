import { common } from './common'
import { home } from './home'
import { nav } from './nav'
import { search } from './search'
import { settings } from './settings'
import { setup } from './setup'
import { skillset } from './skillset'

export const en = {
  common,
  home,
  nav,
  search,
  settings,
  setup,
  skillset,
}

/**
 * The shape every locale must satisfy. `da/index.ts` annotates itself with this, so a missing key,
 * an extra key, or a mismatched interpolation signature is a build error rather than a runtime
 * fallback — which is what `tsc -b` (run by the release publish) enforces in CI.
 */
export type Messages = typeof en
export type Namespace = keyof Messages
