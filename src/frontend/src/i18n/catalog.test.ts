import { describe, it, expect } from 'vitest'
import { en } from './en'
import { da } from './da'
import { LOCALES } from './locale'

type Node = Record<string, unknown>

function paths(node: Node, prefix = ''): Map<string, string> {
  const out = new Map<string, string>()
  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof value === 'object' && value !== null) {
      for (const [p, t] of paths(value as Node, path)) out.set(p, t)
    } else {
      out.set(path, typeof value)
    }
  }
  return out
}

// tsc already guarantees this shape (da/index.ts is annotated with the en catalog's type), and
// that check runs in CI via the release publish. This is the same guarantee at runtime, where a
// human sees it — and it catches anything that slipped through an `as` cast.
describe('catalog parity', () => {
  const enPaths = paths(en as unknown as Node)
  const daPaths = paths(da as unknown as Node)

  it('covers every supported locale', () => {
    expect([...LOCALES].sort()).toEqual(['da', 'en'])
  })

  it('has the same key set in both locales', () => {
    expect([...daPaths.keys()].sort()).toEqual([...enPaths.keys()].sort())
  })

  it('has the same value kind for every key', () => {
    const mismatched = [...enPaths].filter(([path, kind]) => daPaths.get(path) !== kind)
    expect(mismatched).toEqual([])
  })

  it('has no empty Danish strings', () => {
    const empty = [...paths(da as unknown as Node)]
      .filter(([, kind]) => kind === 'string')
      .map(([path]) => path)
      .filter((path) => {
        const value = path.split('.').reduce<unknown>((n, k) => (n as Node)[k], da)
        return typeof value === 'string' && value.trim() === ''
      })
    expect(empty).toEqual([])
  })
})

describe('danish interpolation', () => {
  it('renders counts and singular/plural forms', () => {
    expect(da.settings.restored(1, 0)).toBe('Gendannede 1 fil.')
    expect(da.settings.restored(3, 2)).toBe('Gendannede 3 filer. (2 sprunget over)')
    expect(da.history.deleted(1, 0)).toBe('Slettede 1 søgning')
    expect(da.history.deleted(4, 1)).toBe('Slettede 4 søgninger (1 sprunget over)')
    expect(da.cv.applyFields(1)).toBe('Anvend 1 felt')
    expect(da.cv.applyFields(5)).toBe('Anvend 5 felter')
  })

  it('keeps interpolated values in place', () => {
    expect(da.providers.added('Jobindex')).toContain('Jobindex')
    expect(da.setup.step(2, 2)).toBe('trin 2 af 2')
  })
})
