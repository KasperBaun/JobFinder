import { describe, expect, it } from 'vitest'
import {
  DEFAULT_FILTERS,
  decodeFromHash,
  encodeToHash,
  isDefault,
  withScoreMax,
  withScoreMin,
  type LonglistFilters,
  type LonglistState,
} from './filterState'
import { DEFAULT_SORT, type LonglistSort } from './sortState'

function hash(state: LonglistState): string {
  return encodeToHash(state).toString()
}

function decode(query: string): LonglistState {
  return decodeFromHash(new URLSearchParams(query))
}

const DEFAULT_STATE: LonglistState = { filters: DEFAULT_FILTERS, sort: DEFAULT_SORT }

describe('isDefault', () => {
  it('is true for the pristine filters', () => {
    expect(isDefault(DEFAULT_FILTERS)).toBe(true)
  })

  it('ignores the sort entirely, so changing only the sort leaves the filters pristine', () => {
    // The whole point of splitting sort out: the "Reset filters" link keys off this.
    const state: LonglistState = { filters: DEFAULT_FILTERS, sort: { key: 'posted', dir: 'asc' } }
    expect(isDefault(state.filters)).toBe(true)
  })

  it('is false once any filter moves', () => {
    const cases: Partial<LonglistFilters>[] = [
      { q: 'engineer' },
      { portals: ['itjobbank'] },
      { posted: '7d' },
      { scoreMin: 0.2 },
      { scoreMax: 0.8 },
      { stackHits: ['.NET'] },
      { mark: 'good' },
      { shortlistOnly: true },
    ]
    for (const over of cases) {
      expect(isDefault({ ...DEFAULT_FILTERS, ...over })).toBe(false)
    }
  })
})

describe('rating window', () => {
  it('pins the minimum at the maximum instead of inverting the range', () => {
    const f = { ...DEFAULT_FILTERS, scoreMin: 0.2, scoreMax: 0.6 }
    expect(withScoreMin(f, 0.9).scoreMin).toBe(0.6)
    expect(withScoreMin(f, 0.9).scoreMax).toBe(0.6)
  })

  it('pins the maximum at the minimum instead of inverting the range', () => {
    const f = { ...DEFAULT_FILTERS, scoreMin: 0.4, scoreMax: 0.8 }
    expect(withScoreMax(f, 0.1).scoreMax).toBe(0.4)
    expect(withScoreMax(f, 0.1).scoreMin).toBe(0.4)
  })

  it('moves a thumb freely inside the window', () => {
    const f = { ...DEFAULT_FILTERS, scoreMin: 0.2, scoreMax: 0.8 }
    expect(withScoreMin(f, 0.5).scoreMin).toBe(0.5)
    expect(withScoreMax(f, 0.5).scoreMax).toBe(0.5)
  })

  it('clamps to 0..1', () => {
    expect(withScoreMin(DEFAULT_FILTERS, -2).scoreMin).toBe(0)
    expect(withScoreMax(DEFAULT_FILTERS, 9).scoreMax).toBe(1)
  })

  it('never produces a window that matches nothing', () => {
    let f = DEFAULT_FILTERS
    for (const v of [0.9, 0.1, 1, 0, 0.5, 0.5]) {
      f = withScoreMin(f, v)
      expect(f.scoreMin).toBeLessThanOrEqual(f.scoreMax)
      f = withScoreMax(f, v)
      expect(f.scoreMin).toBeLessThanOrEqual(f.scoreMax)
    }
  })
})

describe('encodeToHash', () => {
  it('emits only the tab for pristine state, so a clean view has a clean URL', () => {
    expect(hash(DEFAULT_STATE)).toBe('tab=longlist')
  })

  it('omits the default sort but keeps a non-default one', () => {
    expect(hash(DEFAULT_STATE)).not.toContain('sort=')
    expect(hash({ ...DEFAULT_STATE, sort: { key: 'company', dir: 'asc' } })).toContain('sort=company-asc')
  })

  it('carries filters and sort together', () => {
    const params = encodeToHash({
      filters: { ...DEFAULT_FILTERS, q: 'engineer', posted: '7d' },
      sort: { key: 'posted', dir: 'asc' },
    })
    expect(params.get('q')).toBe('engineer')
    expect(params.get('posted')).toBe('7d')
    expect(params.get('sort')).toBe('posted-asc')
  })
})

describe('decodeFromHash', () => {
  it('round-trips every filter alongside a non-default sort', () => {
    const state: LonglistState = {
      filters: {
        q: 'senior engineer',
        portals: ['itjobbank', 'jobindex'],
        posted: '14d',
        scoreMin: 0.25,
        scoreMax: 0.75,
        stackHits: ['.NET'],
        mark: 'good',
        shortlistOnly: true,
      },
      sort: { key: 'location', dir: 'desc' },
    }
    expect(decode(hash(state))).toEqual(state)
  })

  it('round-trips every sort key', () => {
    for (const key of ['score', 'title', 'company', 'portal', 'location', 'posted', 'mark'] as const) {
      for (const dir of ['asc', 'desc'] as const) {
        const sort: LonglistSort = { key, dir }
        expect(decode(hash({ ...DEFAULT_STATE, sort })).sort).toEqual(sort)
      }
    }
  })

  it('defaults the sort when the hash carries none', () => {
    expect(decode('tab=longlist').sort).toEqual(DEFAULT_SORT)
    expect(decode('tab=longlist&q=engineer').sort).toEqual(DEFAULT_SORT)
  })

  it('still decodes a sort bookmarked before the split', () => {
    expect(decode('tab=longlist&sort=posted-asc').sort).toEqual({ key: 'posted', dir: 'asc' })
  })

  it('falls back to the default sort on junk without dropping the filters', () => {
    const state = decode('tab=longlist&q=engineer&sort=bogus-sideways')
    expect(state.sort).toEqual(DEFAULT_SORT)
    expect(state.filters.q).toBe('engineer')
  })

  it('ignores a sort key that no longer exists', () => {
    // `stack` was considered and dropped; an old bookmark must not break the view.
    expect(decode('tab=longlist&sort=stack-desc').sort).toEqual(DEFAULT_SORT)
  })

  it('clamps a score range from outside 0..1', () => {
    const { filters } = decode('tab=longlist&score=-3.00-9.00')
    expect(filters.scoreMin).toBe(0)
    expect(filters.scoreMax).toBe(1)
  })

  it('ignores unknown values for enum-shaped filters', () => {
    expect(decode('tab=longlist&posted=forever&mark=maybe').filters).toEqual(DEFAULT_FILTERS)
  })
})
