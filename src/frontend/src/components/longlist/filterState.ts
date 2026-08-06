import type { ScoredEntry } from '../../api/types'
import type { Marks } from './sortRows'
import { decodeSort, encodeSort, type LonglistSort } from './sortState'

export type LonglistFilters = {
  q: string
  portals: string[]            // empty = all
  posted: 'any' | '24h' | '7d' | '14d' | '30d'
  scoreMin: number             // 0..1
  scoreMax: number             // 0..1
  stackHits: string[]          // empty = all (OR semantics across selected)
  mark: 'all' | 'good' | 'bad' | 'unmarked'
  shortlistOnly: boolean
}

/** Which rows to show, and in what order. Two independent axes — resetting one leaves the other. */
export type LonglistState = {
  filters: LonglistFilters
  sort: LonglistSort
}

export const DEFAULT_FILTERS: LonglistFilters = {
  q: '',
  portals: [],
  posted: 'any',
  scoreMin: 0,
  scoreMax: 1,
  stackHits: [],
  mark: 'all',
  shortlistOnly: false,
}

// Sort is deliberately not part of this. It used to be, which made the "Reset filters" link appear
// when only the sort had changed — and clicking it silently threw the sort away.
export function isDefault(f: LonglistFilters): boolean {
  return f.q === ''
    && f.portals.length === 0
    && f.posted === 'any'
    && f.scoreMin === 0 && f.scoreMax === 1
    && f.stackHits.length === 0
    && f.mark === 'all'
    && !f.shortlistOnly
}

export function filterRows(
  rows: readonly ScoredEntry[],
  f: LonglistFilters,
  marks: Marks,
  shortlistIds: ReadonlySet<string>,
): ScoredEntry[] {
  const q = f.q.trim().toLowerCase()
  const portals = new Set(f.portals)
  const stack = new Set(f.stackHits.map((s) => s.toLowerCase()))
  const cutoff = postedCutoff(f.posted)

  return rows.filter((r) => {
    if (q && !(`${r.title} ${r.company ?? ''}`.toLowerCase().includes(q))) return false
    if (portals.size && !portals.has(r.portal)) return false
    if (cutoff && (!r.postedAt || new Date(r.postedAt).getTime() < cutoff)) return false
    if (r.score < f.scoreMin || r.score > f.scoreMax) return false
    if (stack.size) {
      const hits = [...r.primaryStackHits, ...r.secondaryStackHits].map((s) => s.toLowerCase())
      if (!hits.some((h) => stack.has(h))) return false
    }
    if (f.mark !== 'all') {
      const m = marks[r.id]
      if (f.mark === 'unmarked' ? m !== undefined : m !== f.mark) return false
    }
    if (f.shortlistOnly && !shortlistIds.has(r.id)) return false
    return true
  })
}

function postedCutoff(p: LonglistFilters['posted']): number | null {
  if (p === 'any') return null
  const days = p === '24h' ? 1 : p === '7d' ? 7 : p === '14d' ? 14 : 30
  return Date.now() - days * 86_400_000
}

export function encodeToHash(s: LonglistState): URLSearchParams {
  const f = s.filters
  const p = new URLSearchParams()
  p.set('tab', 'longlist')
  if (f.q) p.set('q', f.q)
  if (f.portals.length) p.set('portal', f.portals.join(','))
  if (f.posted !== 'any') p.set('posted', f.posted)
  if (f.scoreMin > 0 || f.scoreMax < 1) p.set('score', `${f.scoreMin.toFixed(2)}-${f.scoreMax.toFixed(2)}`)
  if (f.stackHits.length) p.set('stack', f.stackHits.join(','))
  if (f.mark !== 'all') p.set('mark', f.mark)
  if (f.shortlistOnly) p.set('shortlist', 'true')
  const sort = encodeSort(s.sort)
  if (sort) p.set('sort', sort)
  return p
}

export function decodeFromHash(params: URLSearchParams): LonglistState {
  const f = { ...DEFAULT_FILTERS }
  f.q = params.get('q') ?? ''
  const portal = params.get('portal'); if (portal) f.portals = portal.split(',').filter(Boolean)
  const posted = params.get('posted'); if (posted && ['24h','7d','14d','30d'].includes(posted)) f.posted = posted as LonglistFilters['posted']
  const score = params.get('score')
  if (score) {
    const [lo, hi] = score.split('-').map(Number)
    if (Number.isFinite(lo)) f.scoreMin = clamp01(lo)
    if (Number.isFinite(hi)) f.scoreMax = clamp01(hi)
  }
  const stack = params.get('stack'); if (stack) f.stackHits = stack.split(',').filter(Boolean)
  const mark = params.get('mark')
  if (mark && ['good','bad','unmarked'].includes(mark)) f.mark = mark as LonglistFilters['mark']
  if (params.get('shortlist') === 'true') f.shortlistOnly = true
  return { filters: f, sort: decodeSort(params.get('sort')) }
}

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }
