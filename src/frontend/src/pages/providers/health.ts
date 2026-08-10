import type { ProviderSummary, ProviderTestResult } from '../../api/types'
import { formatRelative } from '../../utils/time'
import type { Messages } from '../../i18n'

export type Health = 'working' | 'failing' | 'stale' | 'untested' | 'blocked'

export type SessionTest = { kind: 'pending' } | { kind: 'done'; result: ProviderTestResult }

const STALE_DAYS = 14

// A source needs a key it doesn't have. Search skips it (see ProviderStateMerger), so it's "On but
// won't run" — flag it here instead of letting it read as OK/stale.
export function isBlocked(p: ProviderSummary): boolean {
  return p.enabled && !!p.requiresSecret && !p.hasSecret
}

export function classifyHealth(p: ProviderSummary, sessionTest?: SessionTest): Health {
  if (sessionTest?.kind === 'done') {
    return sessionTest.result.ok ? 'working' : 'failing'
  }
  if (isBlocked(p)) return 'blocked'
  if (!p.lastFetchedAt) return 'untested'
  const ageMs = Date.now() - new Date(p.lastFetchedAt).getTime()
  const stale = ageMs > STALE_DAYS * 24 * 60 * 60 * 1000
  if (stale) return 'stale'
  return (p.lastFetchCount ?? 0) > 0 ? 'working' : 'failing'
}

export function healthMeta(
  p: ProviderSummary,
  session: SessionTest | undefined,
  t: Messages['providers'],
): string {
  if (session?.kind === 'done') {
    return session.result.ok
      ? t.testedOk(session.result.fetchedCount, session.result.durationMs)
      : t.testedFail(session.result.error ?? t.failedShort)
  }
  if (isBlocked(p)) return t.blockedMeta
  if (p.lastFetchedAt) return t.fetchedMeta(formatRelative(p.lastFetchedAt), p.lastFetchCount)
  return t.neverUsed
}

export function nameById(list: ProviderSummary[] | undefined, id: number): string {
  return list?.find((p) => p.id === id)?.displayName ?? `#${id}`
}

export function truncate(s: string, max: number): string {
  if (s.length <= max) return s
  return s.slice(0, max - 1) + '…'
}

export function friendlyType(type: string, t: Messages['providers']): string {
  return t.type[type as keyof Messages['providers']['type']] ?? type
}
