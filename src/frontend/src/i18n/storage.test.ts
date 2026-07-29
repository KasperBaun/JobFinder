import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { initialLocale, readStoredLocale, writeStoredLocale } from './storage'

const KEY = 'jobfinder.lang'

function setBrowserLanguage(tag: string) {
  vi.spyOn(navigator, 'language', 'get').mockReturnValue(tag)
  vi.spyOn(navigator, 'languages', 'get').mockReturnValue([tag])
}

beforeEach(() => localStorage.clear())
afterEach(() => vi.restoreAllMocks())

describe('stored locale', () => {
  it('round-trips a supported locale', () => {
    writeStoredLocale('da')
    expect(readStoredLocale()).toBe('da')
  })

  it('ignores an unsupported stored value', () => {
    localStorage.setItem(KEY, 'klingon')
    expect(readStoredLocale()).toBeNull()
  })

  it('survives storage being unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('denied')
    })
    expect(readStoredLocale()).toBeNull()
  })
})

describe('boot precedence', () => {
  it('prefers an explicit stored choice over the browser locale', () => {
    setBrowserLanguage('en-US')
    writeStoredLocale('da')
    expect(initialLocale()).toBe('da')
  })

  it('falls back to the browser locale when nothing is stored', () => {
    setBrowserLanguage('da-DK')
    expect(initialLocale()).toBe('da')
  })

  it('falls back to English for any other browser locale', () => {
    setBrowserLanguage('de-DE')
    expect(initialLocale()).toBe('en')
  })
})
