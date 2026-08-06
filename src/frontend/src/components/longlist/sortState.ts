export type SortKey = 'score' | 'title' | 'company' | 'portal' | 'location' | 'posted' | 'mark'
export type SortDir = 'asc' | 'desc'
export type LonglistSort = { key: SortKey; dir: SortDir }

// The single source of truth: the sort-bar options, the sortable-header list and the hash
// validator all read this. The key list used to be spelled out twice, which is how a new key
// gets accepted by the type and rejected by the decoder.
export const SORT_KEYS = ['score', 'title', 'company', 'portal', 'location', 'posted', 'mark'] as const

export const DEFAULT_SORT: LonglistSort = { key: 'score', dir: 'desc' }

// Keys whose interesting end is the high one, so a first click sorts down: best rating, newest
// posting, jobs you liked. Text keys start at A→Z.
const DESC_FIRST_KEYS: ReadonlySet<SortKey> = new Set<SortKey>(['score', 'posted', 'mark'])

export function isSortKey(value: string): value is SortKey {
  return (SORT_KEYS as readonly string[]).includes(value)
}

export function isDefaultSort(sort: LonglistSort): boolean {
  return sort.key === DEFAULT_SORT.key && sort.dir === DEFAULT_SORT.dir
}

// Two states, not the three ApplicationsPage cycles through. Its third state is meaningful
// because its unsorted order is the server's activity order; `RunDetail.scored` is in raw
// pipeline order, monotonic in neither direction, so a "none" here would render an order that
// is neither nameable in the sort bar nor useful. "Reset sort" covers the way back.
export function toggleSort(current: LonglistSort, key: SortKey): LonglistSort {
  if (current.key === key) return flipDir(current)
  return { key, dir: DESC_FIRST_KEYS.has(key) ? 'desc' : 'asc' }
}

export function flipDir(sort: LonglistSort): LonglistSort {
  return { key: sort.key, dir: sort.dir === 'asc' ? 'desc' : 'asc' }
}

/** Wire form for the URL hash, or null when the sort is the default and can be omitted. */
export function encodeSort(sort: LonglistSort): string | null {
  return isDefaultSort(sort) ? null : `${sort.key}-${sort.dir}`
}

// Split on the LAST hyphen, so a key containing one can never be mis-parsed. The wire format is
// deliberately untranslated and may be bookmarked, so it stays `key-dir`.
export function decodeSort(raw: string | null): LonglistSort {
  if (!raw) return DEFAULT_SORT
  const cut = raw.lastIndexOf('-')
  if (cut <= 0) return DEFAULT_SORT
  const key = raw.slice(0, cut)
  const dir = raw.slice(cut + 1)
  if (!isSortKey(key) || (dir !== 'asc' && dir !== 'desc')) return DEFAULT_SORT
  return { key, dir }
}
