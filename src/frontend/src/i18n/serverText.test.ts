import { describe, it, expect, afterEach } from 'vitest'
import * as fs from 'node:fs'
import * as path from 'node:path'
import { setActiveLocale } from './active'
import { serverText } from './serverText'
import { en } from './en'
import { da } from './da'

afterEach(() => setActiveLocale('en'))

describe('serverText', () => {
  it('renders a known key in the active language', () => {
    const args = { provider: 'jobindex', count: 1240, seconds: 12 }
    expect(serverText(en.server.timeline, 'providerDone', args, 'fallback'))
      .toBe('jobindex: 1,240 jobs · 12s')

    setActiveLocale('da')
    expect(serverText(da.server.timeline, 'providerDone', args, 'fallback'))
      .toBe('jobindex: 1.240 job · 12 s')
  })

  // Runs recorded before the keys existed carry only prose. They must keep rendering as they
  // always did rather than disappearing or showing a raw key.
  it('falls back to the stored prose when there is no key', () => {
    expect(serverText(en.server.timeline, undefined, undefined, 'Search queued')).toBe('Search queued')
    expect(serverText(da.server.timeline, null, null, 'Search queued')).toBe('Search queued')
  })

  it('falls back when the key is one this build does not know', () => {
    expect(serverText(da.server.timeline, 'somethingNewerThanThisBuild', {}, 'the prose'))
      .toBe('the prose')
  })

  it('tolerates missing or wrongly-typed args instead of throwing', () => {
    expect(() => serverText(da.server.timeline, 'providerDone', {}, '')).not.toThrow()
    expect(() => serverText(da.server.reasoning, 'primaryHits', { skills: 'not-an-array' }, '')).not.toThrow()
  })
})

describe('server catalog', () => {
  it('renders the ranking rationale in Danish', () => {
    setActiveLocale('da')
    const notes = [
      { key: 'primaryHits', args: { skills: ['C#', '.NET'] } },
      { key: 'seniorityMatches', args: {} },
      { key: 'agePenalty', args: { days: 41 } },
    ]
    const text = notes.map(nt => serverText(da.server.reasoning, nt.key, nt.args, '')).join(' ')
    expect(text).toBe(
      'Match på skal-have-kompetencer: C#, .NET. Erfaringsniveauet passer. '
      + 'Slået op for 41 dage siden — scoren er sat ned for alder.',
    )
  })

  it('covers every key the backend can emit, in both locales', () => {
    for (const group of ['timeline', 'reasoning'] as const) {
      expect(Object.keys(da.server[group]).sort()).toEqual(Object.keys(en.server[group]).sort())
    }
  })
})

describe('English wording agrees with the backend', () => {
  // The wording exists in two implementations by necessity: Ranker.Notes.cs renders top-jobs.md and
  // the persisted prose, this catalog renders the UI. src/tests/fixtures/reasoning-en.json is the
  // shared golden file — RankerNotesTests asserts the C# side against the very same entries, so a
  // change to either implementation alone fails a test. They had already drifted on apostrophes.
  const fixture = JSON.parse(
    fs.readFileSync(path.resolve(__dirname, '../../../tests/fixtures/reasoning-en.json'), 'utf-8'),
  ) as { notes: { key: string; args: Record<string, unknown>; english: string }[] }

  it('matches the shared English fixture for every key', () => {
    setActiveLocale('en')
    for (const { key, args, english } of fixture.notes) {
      expect(serverText(en.server.reasoning, key, args, '<<no renderer>>')).toBe(english)
    }
  })

  it('covers every key the fixture pins', () => {
    expect(fixture.notes).toHaveLength(Object.keys(en.server.reasoning).length)
  })
})
