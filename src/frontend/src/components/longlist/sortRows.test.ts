import { afterEach, describe, expect, it } from 'vitest'
import type { ScoredEntry } from '../../api/types'
import { setActiveLocale } from '../../i18n/active'
import { sortRows, type Marks } from './sortRows'
import type { SortKey } from './sortState'

function entry(over: Partial<ScoredEntry> & { id: string }): ScoredEntry {
  return {
    title: `title-${over.id}`,
    url: `https://example.test/${over.id}`,
    portal: 'itjobbank',
    score: 0.5,
    breakdown: {
      primaryStack: 0, secondaryStack: 0, seniority: 0,
      locationRemote: 0, domain: 0, freshness: 0, disqualifierPenalty: 0,
    },
    primaryStackHits: [],
    secondaryStackHits: [],
    ...over,
  }
}

const NO_MARKS: Marks = {}

function ids(rows: readonly ScoredEntry[]): string[] {
  return rows.map((r) => r.id)
}

function sorted(rows: readonly ScoredEntry[], key: SortKey, dir: 'asc' | 'desc', marks: Marks = NO_MARKS) {
  return ids(sortRows(rows, { key, dir }, marks))
}

afterEach(() => setActiveLocale('en'))

describe('sortRows', () => {
  it('does not mutate the input', () => {
    const rows = [entry({ id: 'b', score: 0.1 }), entry({ id: 'a', score: 0.9 })]
    sortRows(rows, { key: 'score', dir: 'desc' }, NO_MARKS)
    expect(ids(rows)).toEqual(['b', 'a'])
  })

  it('sorts by rating in both directions', () => {
    const rows = [
      entry({ id: 'mid', score: 0.5 }),
      entry({ id: 'high', score: 0.9 }),
      entry({ id: 'low', score: 0.1 }),
    ]
    expect(sorted(rows, 'score', 'desc')).toEqual(['high', 'mid', 'low'])
    expect(sorted(rows, 'score', 'asc')).toEqual(['low', 'mid', 'high'])
  })

  it('sorts by posting date newest-first descending', () => {
    const rows = [
      entry({ id: 'old', postedAt: '2026-01-01T00:00:00Z' }),
      entry({ id: 'new', postedAt: '2026-08-01T00:00:00Z' }),
    ]
    expect(sorted(rows, 'posted', 'desc')).toEqual(['new', 'old'])
    expect(sorted(rows, 'posted', 'asc')).toEqual(['old', 'new'])
  })

  it('compares dates as instants, so mixed UTC offsets order correctly', () => {
    // A real run mixes +00:00 and -04:00 timestamps. Compared as strings, the -04:00 row sorts
    // first because '-' follows '+' in ASCII, even though it is the later instant.
    const rows = [
      entry({ id: 'later', postedAt: '2026-06-05T08:48:05-04:00' }),   // 12:48 UTC
      entry({ id: 'earlier', postedAt: '2026-06-05T10:10:50.666+00:00' }),
    ]
    expect(sorted(rows, 'posted', 'asc')).toEqual(['earlier', 'later'])
    expect(sorted(rows, 'posted', 'desc')).toEqual(['later', 'earlier'])
  })

  it('ignores inconsistent fractional seconds', () => {
    const rows = [
      entry({ id: 'second', postedAt: '2026-06-05T10:10:51+00:00' }),
      entry({ id: 'first', postedAt: '2026-06-05T10:10:50.666+00:00' }),
    ]
    expect(sorted(rows, 'posted', 'asc')).toEqual(['first', 'second'])
  })

  it('treats an unparseable timestamp as missing rather than as NaN', () => {
    const rows = [
      entry({ id: 'junk', postedAt: 'not-a-date', score: 0.5 }),
      entry({ id: 'dated', postedAt: '2026-06-05T10:00:00Z', score: 0.5 }),
    ]
    expect(sorted(rows, 'posted', 'asc')).toEqual(['dated', 'junk'])
    expect(sorted(rows, 'posted', 'desc')).toEqual(['dated', 'junk'])
  })

  it('falls back to the portal slug when the display name is missing', () => {
    const rows = [
      entry({ id: 'named', portal: 'zzz', portalDisplayName: 'Aaa Portal' }),
      entry({ id: 'slug', portal: 'bbb' }),
    ]
    expect(sorted(rows, 'portal', 'asc')).toEqual(['named', 'slug'])
  })

  describe('missing values', () => {
    it('sorts listings without a company last in BOTH directions', () => {
      const rows = [
        entry({ id: 'blank', score: 0.5 }),
        entry({ id: 'acme', company: 'Acme', score: 0.5 }),
        entry({ id: 'zeta', company: 'Zeta', score: 0.5 }),
      ]
      expect(sorted(rows, 'company', 'asc')).toEqual(['acme', 'zeta', 'blank'])
      expect(sorted(rows, 'company', 'desc')).toEqual(['zeta', 'acme', 'blank'])
    })

    it('sorts listings without a posting date last in BOTH directions', () => {
      const rows = [
        entry({ id: 'undated', score: 0.5 }),
        entry({ id: 'old', postedAt: '2026-01-01T00:00:00Z', score: 0.5 }),
        entry({ id: 'new', postedAt: '2026-08-01T00:00:00Z', score: 0.5 }),
      ]
      expect(sorted(rows, 'posted', 'asc')).toEqual(['old', 'new', 'undated'])
      expect(sorted(rows, 'posted', 'desc')).toEqual(['new', 'old', 'undated'])
    })

    it('sorts listings without a location last in BOTH directions', () => {
      const rows = [
        entry({ id: 'nowhere', score: 0.5 }),
        entry({ id: 'aarhus', location: 'Aarhus', score: 0.5 }),
      ]
      expect(sorted(rows, 'location', 'asc')).toEqual(['aarhus', 'nowhere'])
      expect(sorted(rows, 'location', 'desc')).toEqual(['aarhus', 'nowhere'])
    })

    it('treats an empty string like a missing value', () => {
      const rows = [entry({ id: 'empty', company: '', score: 0.5 }), entry({ id: 'named', company: 'Acme', score: 0.5 })]
      expect(sorted(rows, 'company', 'asc')).toEqual(['named', 'empty'])
    })
  })

  describe('your rating', () => {
    const rows = [
      entry({ id: 'bad', score: 0.5 }),
      entry({ id: 'unrated', score: 0.5 }),
      entry({ id: 'good', score: 0.5 }),
    ]
    const marks: Marks = { good: 'good', bad: 'bad' }

    it('orders good ▸ not rated ▸ bad ascending', () => {
      expect(sorted(rows, 'mark', 'asc', marks)).toEqual(['good', 'unrated', 'bad'])
    })

    it('reverses to bad ▸ not rated ▸ good descending', () => {
      expect(sorted(rows, 'mark', 'desc', marks)).toEqual(['bad', 'unrated', 'good'])
    })

    it('collapses to the tie-break when nothing is rated', () => {
      expect(sorted(rows, 'mark', 'desc')).toEqual(sorted(rows, 'mark', 'asc'))
    })
  })

  describe('tie-break', () => {
    it('orders a tie group by rating, then title, then id', () => {
      const rows = [
        entry({ id: 'c', company: 'Acme', score: 0.4, title: 'Beta' }),
        entry({ id: 'b', company: 'Acme', score: 0.4, title: 'Alpha' }),
        entry({ id: 'a', company: 'Acme', score: 0.9, title: 'Zeta' }),
      ]
      expect(sorted(rows, 'company', 'asc')).toEqual(['a', 'b', 'c'])
    })

    it('breaks a title tie by id so the order is total', () => {
      const rows = [
        entry({ id: 'z', title: 'Same', score: 0.4 }),
        entry({ id: 'a', title: 'Same', score: 0.4 }),
      ]
      expect(sorted(rows, 'title', 'asc')).toEqual(['a', 'z'])
      expect(sorted(rows, 'title', 'desc')).toEqual(['a', 'z'])
    })

    it('does not reshuffle a tie group when the direction flips', () => {
      const rows = [
        entry({ id: 'a1', company: 'Acme', score: 0.9 }),
        entry({ id: 'a2', company: 'Acme', score: 0.2 }),
        entry({ id: 'z1', company: 'Zeta', score: 0.5 }),
      ]
      // The Acme block moves to the end but stays rating-descending inside.
      expect(sorted(rows, 'company', 'asc')).toEqual(['a1', 'a2', 'z1'])
      expect(sorted(rows, 'company', 'desc')).toEqual(['z1', 'a1', 'a2'])
    })

    it('compares raw ratings, not the two decimals the cell shows', () => {
      const rows = [
        entry({ id: 'lower', company: 'Acme', score: 0.1751, title: 'Zeta' }),
        entry({ id: 'higher', company: 'Acme', score: 0.1759, title: 'Alpha' }),
      ]
      // Both render "0.18". Rounding first would hand the group to the title compare.
      expect(sorted(rows, 'company', 'asc')).toEqual(['higher', 'lower'])
    })

    it('is stable across repeated sorts of the same rows in any input order', () => {
      const build = (order: string[]) =>
        order.map((id) => entry({ id, company: 'Acme', score: 0.5, title: 'Same' }))
      const forwards = sortRows(build(['a', 'b', 'c']), { key: 'company', dir: 'asc' }, NO_MARKS)
      const backwards = sortRows(build(['c', 'b', 'a']), { key: 'company', dir: 'asc' }, NO_MARKS)
      expect(ids(forwards)).toEqual(ids(backwards))
    })
  })

  it('collates Danish æ/ø/å after z in the Danish locale', () => {
    setActiveLocale('da')
    const rows = [
      entry({ id: 'aa', company: 'Ågård', score: 0.5 }),
      entry({ id: 'zz', company: 'Zeta', score: 0.5 }),
    ]
    expect(sorted(rows, 'company', 'asc')).toEqual(['zz', 'aa'])
  })
})
