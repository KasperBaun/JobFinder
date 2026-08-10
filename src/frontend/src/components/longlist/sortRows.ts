import type { RunDetail, ScoredEntry } from '../../api/types'
import { activeLocale } from '../../i18n/active'
import { collator } from '../../i18n/format'
import type { LonglistSort, SortKey } from './sortState'

/** Derived from the API type so the two can't drift; a run's marks live beside `scored`, not on it. */
export type Marks = Readonly<RunDetail['marks']>
type MarkValue = Marks[string]

// Ascending reads "what did I like" first. Unrated sits in the middle because it is the absence
// of a judgement, not a bad one.
const MARK_RANK = { good: 0, unmarked: 1, bad: 2 } as const

/**
 * Total order over the longlist. Missing values sort last in both directions, and every
 * comparison falls through to the same tie-break, so identical filters and sort always produce
 * identical row order.
 */
export function sortRows(
  rows: readonly ScoredEntry[],
  sort: LonglistSort,
  marks: Marks,
): ScoredEntry[] {
  const sign = sort.dir === 'asc' ? 1 : -1
  // One collator for the whole sort rather than a fresh one per comparison, and Danish collation
  // so æ/ø/å land after z instead of wherever the host OS decides.
  const text = collator(activeLocale())
  return [...rows].sort((a, b) => {
    const primary = comparePrimary(a, b, sort.key, sign, text, marks)
    return primary !== 0 ? primary : tieBreak(a, b, text)
  })
}

function comparePrimary(
  a: ScoredEntry,
  b: ScoredEntry,
  key: SortKey,
  sign: number,
  text: Intl.Collator,
  marks: Marks,
): number {
  switch (key) {
    case 'score':    return sign * (a.score - b.score)
    case 'title':    return sign * text.compare(a.title, b.title)
    case 'company':  return compareText(a.company, b.company, sign, text)
    case 'location': return compareText(a.location, b.location, sign, text)
    // Always present — the server falls back to the portal slug when it has no display name.
    case 'portal':   return sign * text.compare(a.portalDisplayName ?? a.portal, b.portalDisplayName ?? b.portal)
    case 'posted':   return comparePosted(a.postedAt, b.postedAt, sign)
    case 'mark':     return sign * (markRank(marks[a.id]) - markRank(marks[b.id]))
  }
}

function markRank(mark: MarkValue | undefined): number {
  return MARK_RANK[mark ?? 'unmarked']
}

// The missing-value branches return unsigned, so blanks land last whichever way the column is
// pointing — the convention ApplicationsPage.sortByStatusSet already set. Coalescing to '' instead
// floats them to the top ascending, and on a real run 800 of 2 089 listings have no postedAt.
function compareText(
  a: string | undefined,
  b: string | undefined,
  sign: number,
  text: Intl.Collator,
): number {
  if (!a && !b) return 0
  if (!a) return 1
  if (!b) return -1
  return sign * text.compare(a, b)
}

// Parsed to instants, not compared as strings. Adapters preserve each source's own UTC offset, so a
// single run mixes forms — a real 2 089-listing run held 735 `+00:00` and 554 `-04:00` timestamps,
// with fractional seconds on some and not others. Lexicographically "…T08:48:05-04:00" (12:48 UTC)
// sorts before "…T10:10:50.666+00:00", which is the wrong way round, and every `-04:00` row sorts
// after an equal-prefix `+00:00` one purely because '-' follows '+' in ASCII.
// An unparseable timestamp is treated as missing rather than as NaN, which would compare false
// against everything and leave the order intransitive.
function comparePosted(a: string | undefined, b: string | undefined, sign: number): number {
  const ta = instant(a)
  const tb = instant(b)
  if (ta === null && tb === null) return 0
  if (ta === null) return 1
  if (tb === null) return -1
  return sign * (ta - tb)
}

function instant(value: string | undefined): number | null {
  if (!value) return null
  const parsed = Date.parse(value)
  return Number.isNaN(parsed) ? null : parsed
}

// Direction-independent by design: flipping asc/desc moves a tie group to the other end of the
// table but must not reshuffle it internally. Rating comes first because it is the ranker's own
// order — the order the shortlist was cut from — and it is compared raw, never rounded to the two
// decimals the cell shows: rows that all read "0.18" hold genuinely different values.
function tieBreak(a: ScoredEntry, b: ScoredEntry, text: Intl.Collator): number {
  if (a.score !== b.score) return b.score - a.score
  const byTitle = text.compare(a.title, b.title)
  if (byTitle !== 0) return byTitle
  return a.id < b.id ? -1 : a.id > b.id ? 1 : 0
}
