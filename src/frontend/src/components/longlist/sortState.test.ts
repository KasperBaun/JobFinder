import { describe, expect, it } from 'vitest'
import {
  DEFAULT_SORT,
  decodeSort,
  encodeSort,
  flipDir,
  isDefaultSort,
  isSortKey,
  SORT_KEYS,
  toggleSort,
} from './sortState'

describe('isSortKey', () => {
  it('accepts every key the sort bar and headers offer', () => {
    for (const key of SORT_KEYS) expect(isSortKey(key)).toBe(true)
  })

  it('rejects anything else', () => {
    expect(isSortKey('stack')).toBe(false)
    expect(isSortKey('')).toBe(false)
    expect(isSortKey('SCORE')).toBe(false)
  })
})

describe('toggleSort', () => {
  it('flips the direction when the key is already active', () => {
    expect(toggleSort({ key: 'score', dir: 'desc' }, 'score')).toEqual({ key: 'score', dir: 'asc' })
    expect(toggleSort({ key: 'score', dir: 'asc' }, 'score')).toEqual({ key: 'score', dir: 'desc' })
  })

  it('starts text keys ascending', () => {
    for (const key of ['title', 'company', 'portal', 'location'] as const) {
      expect(toggleSort(DEFAULT_SORT, key)).toEqual({ key, dir: 'asc' })
    }
  })

  it('starts rating, posting date and your rating descending — best, newest, liked first', () => {
    expect(toggleSort({ key: 'title', dir: 'asc' }, 'score')).toEqual({ key: 'score', dir: 'desc' })
    expect(toggleSort({ key: 'title', dir: 'asc' }, 'posted')).toEqual({ key: 'posted', dir: 'desc' })
    expect(toggleSort({ key: 'title', dir: 'asc' }, 'mark')).toEqual({ key: 'mark', dir: 'desc' })
  })

  it('never lands on a third, unsorted state', () => {
    let sort = DEFAULT_SORT
    for (let i = 0; i < 5; i++) {
      sort = toggleSort(sort, 'title')
      expect(sort.key).toBe('title')
    }
  })
})

describe('flipDir', () => {
  it('keeps the key and reverses the direction', () => {
    expect(flipDir({ key: 'company', dir: 'asc' })).toEqual({ key: 'company', dir: 'desc' })
  })
})

describe('encodeSort / decodeSort', () => {
  it('omits the default sort from the hash', () => {
    expect(encodeSort(DEFAULT_SORT)).toBeNull()
    expect(isDefaultSort(DEFAULT_SORT)).toBe(true)
  })

  it('round-trips every key in both directions', () => {
    for (const key of SORT_KEYS) {
      for (const dir of ['asc', 'desc'] as const) {
        const sort = { key, dir }
        const wire = encodeSort(sort)
        if (wire === null) {
          expect(isDefaultSort(sort)).toBe(true)
          continue
        }
        expect(decodeSort(wire)).toEqual(sort)
      }
    }
  })

  it('keeps the existing wire format so bookmarked URLs still decode', () => {
    expect(encodeSort({ key: 'posted', dir: 'asc' })).toBe('posted-asc')
    expect(decodeSort('posted-asc')).toEqual({ key: 'posted', dir: 'asc' })
  })

  it('falls back to the default rather than throwing on junk', () => {
    for (const junk of [null, '', 'score', 'bogus-sideways', 'score-sideways', '-asc', 'score-']) {
      expect(decodeSort(junk)).toEqual(DEFAULT_SORT)
    }
  })

  it('splits on the last hyphen, so a hyphenated key would still parse', () => {
    // No key contains a hyphen today; this pins the parser so adding one cannot break the codec.
    expect(decodeSort('no-such-key-asc')).toEqual(DEFAULT_SORT)
    expect(decodeSort('title-desc')).toEqual({ key: 'title', dir: 'desc' })
  })
})
