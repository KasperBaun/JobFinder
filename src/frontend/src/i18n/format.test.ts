import { describe, it, expect, afterEach } from 'vitest'
import { setActiveLocale } from './active'
import { collator, dateTimeFormat, dec, n } from './format'

afterEach(() => setActiveLocale('en'))

describe('number formatting', () => {
  it('groups thousands per locale', () => {
    setActiveLocale('en')
    expect(n(1234567)).toBe('1,234,567')
    setActiveLocale('da')
    expect(n(1234567)).toBe('1.234.567')
  })

  it('uses the locale decimal separator and fixed precision', () => {
    setActiveLocale('en')
    expect(dec(0.8, 2)).toBe('0.80')
    expect(dec(12.345, 1)).toBe('12.3')
    setActiveLocale('da')
    expect(dec(0.8, 2)).toBe('0,80')
    expect(dec(12.345, 1)).toBe('12,3')
  })
})

describe('danish collation', () => {
  // The whole reason Intl.Collator replaced String.localeCompare: æ, ø and å are distinct letters
  // that sort after z, not variants of a and o.
  it('sorts æ, ø and å after z', () => {
    const words = ['Ålborg', 'Bornholm', 'Zealand', 'Ærø', 'Østerbro']
    const sorted = [...words].sort(collator('da').compare)
    expect(sorted).toEqual(['Bornholm', 'Zealand', 'Ærø', 'Østerbro', 'Ålborg'])
  })

  it('sorts them as accented vowels in English', () => {
    const sorted = ['Zealand', 'Ærø', 'Ålborg'].sort(collator('en').compare)
    expect(sorted[sorted.length - 1]).toBe('Zealand')
  })

  it('orders embedded numbers numerically', () => {
    const sorted = ['Engineer 10', 'Engineer 2'].sort(collator('en').compare)
    expect(sorted).toEqual(['Engineer 2', 'Engineer 10'])
  })
})

describe('date formatting', () => {
  it('produces a different rendering per locale', () => {
    const date = new Date('2026-07-29T14:30:00Z')
    const english = dateTimeFormat('en').format(date)
    const danish = dateTimeFormat('da').format(date)
    expect(english).not.toBe(danish)
    // Danish uses a 24-hour clock, so no AM/PM marker.
    expect(danish).not.toMatch(/[AP]M/i)
  })
})
