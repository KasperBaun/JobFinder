export type SortKey = 'title' | 'company' | 'portal' | 'location' | 'posted' | 'score'

export type LonglistFilters = {
  q: string
  portals: string[]            // empty = all
  posted: 'any' | '24h' | '7d' | '14d' | '30d'
  scoreMin: number             // 0..1
  scoreMax: number             // 0..1
  stackHits: string[]          // empty = all (OR semantics across selected)
  mark: 'all' | 'good' | 'bad' | 'unmarked'
  shortlistOnly: boolean
  sort: { key: SortKey; dir: 'asc' | 'desc' }
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
  sort: { key: 'score', dir: 'desc' },
}

export function isDefault(f: LonglistFilters): boolean {
  return f.q === ''
    && f.portals.length === 0
    && f.posted === 'any'
    && f.scoreMin === 0 && f.scoreMax === 1
    && f.stackHits.length === 0
    && f.mark === 'all'
    && !f.shortlistOnly
    && f.sort.key === 'score' && f.sort.dir === 'desc'
}

export function encodeToHash(f: LonglistFilters): URLSearchParams {
  const p = new URLSearchParams()
  p.set('tab', 'longlist')
  if (f.q) p.set('q', f.q)
  if (f.portals.length) p.set('portal', f.portals.join(','))
  if (f.posted !== 'any') p.set('posted', f.posted)
  if (f.scoreMin > 0 || f.scoreMax < 1) p.set('score', `${f.scoreMin.toFixed(2)}-${f.scoreMax.toFixed(2)}`)
  if (f.stackHits.length) p.set('stack', f.stackHits.join(','))
  if (f.mark !== 'all') p.set('mark', f.mark)
  if (f.shortlistOnly) p.set('shortlist', 'true')
  if (f.sort.key !== 'score' || f.sort.dir !== 'desc') p.set('sort', `${f.sort.key}-${f.sort.dir}`)
  return p
}

export function decodeFromHash(params: URLSearchParams): LonglistFilters {
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
  const sort = params.get('sort')
  if (sort) {
    const [key, dir] = sort.split('-')
    if (['title','company','portal','location','posted','score'].includes(key) && (dir === 'asc' || dir === 'desc')) {
      f.sort = { key: key as SortKey, dir }
    }
  }
  return f
}

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }
