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
}

export const PAGE_SIZES = [50, 100, 150, 200, 250, 300] as const
export const DEFAULT_PAGE_SIZE = 100

/**
 * Which rows to show, in what order, and which slice of them. Filters and sort are independent
 * axes — resetting one leaves the other — while the page is subordinate to both: change what the
 * table holds or how it is ordered and page 5 of the old arrangement means nothing, so the
 * with* helpers below send it back to 1. Use them instead of spreading.
 */
export type LonglistState = {
  filters: LonglistFilters
  sort: LonglistSort
  page: number
  size: number
}

export function withFilters(s: LonglistState, filters: LonglistFilters): LonglistState {
  return { ...s, filters, page: 1 }
}

export function withSort(s: LonglistState, sort: LonglistSort): LonglistState {
  return { ...s, sort, page: 1 }
}

export function withPage(s: LonglistState, page: number): LonglistState {
  return { ...s, page: Math.max(1, Math.floor(page)) }
}

export function withPageSize(s: LonglistState, size: number): LonglistState {
  return { ...s, size, page: 1 }
}

export const DEFAULT_FILTERS: LonglistFilters = {
  q: '',
  portals: [],
  posted: 'any',
  scoreMin: 0,
  scoreMax: 1,
  stackHits: [],
  mark: 'all',
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
}

// Dragging one rating thumb past the other pins it instead of inverting the window. An inverted
// range (min > max) is not a filter anyone means: `filterRows` then matches nothing, and the table
// empties with no indication that the range is the reason.
export function withScoreMin(f: LonglistFilters, value: number): LonglistFilters {
  return { ...f, scoreMin: Math.min(clamp01(value), f.scoreMax) }
}

export function withScoreMax(f: LonglistFilters, value: number): LonglistFilters {
  return { ...f, scoreMax: Math.max(clamp01(value), f.scoreMin) }
}

export function filterRows(
  rows: readonly ScoredEntry[],
  f: LonglistFilters,
  marks: Marks,
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
  const sort = encodeSort(s.sort)
  if (sort) p.set('sort', sort)
  if (s.size !== DEFAULT_PAGE_SIZE) p.set('size', String(s.size))
  if (s.page > 1) p.set('page', String(s.page))
  return p
}

export function decodeFromHash(params: URLSearchParams): LonglistState {
  const f = { ...DEFAULT_FILTERS }
  f.q = params.get('q') ?? ''
  const portal = params.get('portal'); if (portal) f.portals = portal.split(',').filter(Boolean)
  const posted = params.get('posted'); if (posted && ['24h','7d','14d','30d'].includes(posted)) f.posted = posted as LonglistFilters['posted']
  const score = params.get('score')
  if (score) {
    // Splits on every hyphen, unlike decodeSort, which splits on the last. Harmless only because
    // encodeToHash always writes two non-negative toFixed(2) values, so there is exactly one: a
    // hand-edited "-0.2-0.8" parses as ['', '0.2', '0.8'] and silently clamps instead of failing.
    const [lo, hi] = score.split('-').map(Number)
    if (Number.isFinite(lo)) f.scoreMin = clamp01(lo)
    if (Number.isFinite(hi)) f.scoreMax = clamp01(hi)
  }
  const stack = params.get('stack'); if (stack) f.stackHits = stack.split(',').filter(Boolean)
  const mark = params.get('mark')
  if (mark && ['good','bad','unmarked'].includes(mark)) f.mark = mark as LonglistFilters['mark']
  // A `shortlist=true` from a pre-R-085-rework URL is simply ignored: the chip it drove is gone,
  // and the Top jobs view is where that subset lives now.
  const rawSize = Number(params.get('size'))
  const size = (PAGE_SIZES as readonly number[]).includes(rawSize) ? rawSize : DEFAULT_PAGE_SIZE
  const rawPage = Number(params.get('page'))
  const page = Number.isInteger(rawPage) && rawPage > 1 ? rawPage : 1
  return { filters: f, sort: decodeSort(params.get('sort')), page, size }
}

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }
