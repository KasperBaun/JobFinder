import { common } from './common'
import { nav } from './nav'

export const en = {
  common,
  nav,
}

/**
 * The shape every locale must satisfy. `da/index.ts` annotates itself with this, so a missing key,
 * an extra key, or a mismatched interpolation signature is a build error rather than a runtime
 * fallback — which is what `tsc -b` (run by the release publish) enforces in CI.
 */
export type Messages = typeof en
export type Namespace = keyof Messages
