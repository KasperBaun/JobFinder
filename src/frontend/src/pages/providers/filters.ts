export const FILTER_KEYS = ['all', 'on', 'off', 'failing'] as const
export type FilterKey = (typeof FILTER_KEYS)[number]

export type FilterCounts = Record<FilterKey, number>

// The filter chips double as the source summary, so their counts carry the same tone the old
// stats card used: enabled = good, failing = bad, off = muted. Zero failing stays neutral so an
// empty "Failing 0" doesn't read as an alert.
export function countTone(key: FilterKey, counts: FilterCounts): 'good' | 'bad' | 'muted' | undefined {
  if (key === 'on') return counts.on > 0 ? 'good' : undefined
  if (key === 'failing') return counts.failing > 0 ? 'bad' : undefined
  if (key === 'off') return 'muted'
  return undefined
}
