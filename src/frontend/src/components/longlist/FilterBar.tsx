import { useMemo, useState } from 'react'
import type { ScoredEntry } from '../../api/types'
import { activeLocale } from '../../i18n/active'
import { collator, dec, useT } from '../../i18n'
import { DEFAULT_FILTERS, isDefault as filtersIsDefault, type LonglistFilters } from './filterState'
import { FilterPopover } from './FilterPopover'
import { ScoreRange } from './ScoreRange'
import { SourceFilter } from './SourceFilter'

interface Props {
  scored: readonly ScoredEntry[]
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
}

const POSTED_KEYS = ['any', '24h', '7d', '14d', '30d'] as const
const MARK_KEYS = ['all', 'good', 'bad', 'unmarked'] as const

/** Which group's panel is open; only one at a time. */
type OpenGroup = 'source' | 'posted' | 'score' | 'stack' | 'mark' | null

export function FilterBar({ scored, filters, onChange }: Props) {
  const t = useT('history')
  const [open, setOpen] = useState<OpenGroup>(null)
  const toggleOpen = (group: Exclude<OpenGroup, null>) => (next: boolean) => setOpen(next ? group : null)

  // Busiest source first: the long tail of a real run is mostly single-digit, and the panel is
  // scrollable, so leading with the sources that actually carry listings is what makes it scannable.
  const sources = useMemo(() => {
    const counts = new Map<string, { label: string; count: number }>()
    for (const e of scored) {
      const current = counts.get(e.portal)
      if (current) current.count++
      else counts.set(e.portal, { label: e.portalDisplayName ?? e.portal, count: 1 })
    }
    const text = collator(activeLocale())
    return [...counts]
      .map(([slug, v]) => ({ slug, ...v }))
      .sort((a, b) => b.count - a.count || text.compare(a.label, b.label))
  }, [scored])

  const stackHits = useMemo(() => {
    const counts = new Map<string, number>()
    for (const e of scored) {
      for (const h of [...e.primaryStackHits, ...e.secondaryStackHits]) {
        counts.set(h, (counts.get(h) ?? 0) + 1)
      }
    }
    return [...counts].sort(([, a], [, b]) => b - a)
  }, [scored])

  const togglePortal = (p: string) =>
    onChange({
      ...filters,
      portals: filters.portals.includes(p)
        ? filters.portals.filter((x) => x !== p)
        : [...filters.portals, p],
    })
  const toggleStack = (s: string) =>
    onChange({
      ...filters,
      stackHits: filters.stackHits.includes(s)
        ? filters.stackHits.filter((x) => x !== s)
        : [...filters.stackHits, s],
    })

  const markLabel: Record<(typeof MARK_KEYS)[number], string> = {
    all: t.markAll, good: t.markGood, bad: t.markBad, unmarked: t.markUnmarked,
  }
  const scoreFiltered = filters.scoreMin > 0 || filters.scoreMax < 1

  // The free-text search is not here: it leads the toolbar as its own SearchField, on the far
  // side of the view menu, so this bar is purely the collapsed filter groups.
  return (
    <div className="longlist__filter-bar">
      {sources.length > 0 && (
        <FilterPopover
          label={t.filterSource}
          count={filters.portals.length}
          open={open === 'source'}
          onOpenChange={toggleOpen('source')}
          onClear={() => onChange({ ...filters, portals: [] })}
        >
          <SourceFilter sources={sources} selected={filters.portals} onToggle={togglePortal} />
        </FilterPopover>
      )}

      <FilterPopover
        label={t.filterPosted}
        summary={filters.posted === 'any' ? undefined : filters.posted}
        open={open === 'posted'}
        onOpenChange={toggleOpen('posted')}
        onClear={() => onChange({ ...filters, posted: 'any' })}
      >
        <div className="filter-pop__row">
          {POSTED_KEYS.map((k) => (
            <Pill key={k} active={filters.posted === k} onClick={() => onChange({ ...filters, posted: k })}>
              {k === 'any' ? t.filterPostedAny : k}
            </Pill>
          ))}
        </div>
      </FilterPopover>

      <FilterPopover
        label={t.filterScore}
        summary={scoreFiltered ? `${dec(filters.scoreMin, 2)}–${dec(filters.scoreMax, 2)}` : undefined}
        open={open === 'score'}
        onOpenChange={toggleOpen('score')}
        onClear={() => onChange({ ...filters, scoreMin: 0, scoreMax: 1 })}
      >
        <ScoreRange filters={filters} onChange={onChange} />
      </FilterPopover>

      {stackHits.length > 0 && (
        <FilterPopover
          label={t.filterSkillMatch}
          count={filters.stackHits.length}
          open={open === 'stack'}
          onOpenChange={toggleOpen('stack')}
          onClear={() => onChange({ ...filters, stackHits: [] })}
        >
          <div className="filter-pop__row">
            {stackHits.map(([s]) => (
              <Pill key={s} active={filters.stackHits.includes(s)} onClick={() => toggleStack(s)}>
                {s}
              </Pill>
            ))}
          </div>
        </FilterPopover>
      )}

      <FilterPopover
        label={t.filterYourRating}
        summary={filters.mark === 'all' ? undefined : markLabel[filters.mark]}
        open={open === 'mark'}
        onOpenChange={toggleOpen('mark')}
        onClear={() => onChange({ ...filters, mark: 'all' })}
      >
        <div className="filter-pop__row">
          {MARK_KEYS.map((k) => (
            <Pill key={k} active={filters.mark === k} onClick={() => onChange({ ...filters, mark: k })}>
              {markLabel[k]}
            </Pill>
          ))}
        </div>
      </FilterPopover>

      {!filtersIsDefault(filters) && (
        <button type="button" className="link-button" onClick={() => onChange(DEFAULT_FILTERS)}>
          {t.resetFilters}
        </button>
      )}
    </div>
  )
}

function Pill({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" className={`pill ${active ? 'pill--active' : ''}`} aria-pressed={active} onClick={onClick}>
      {children}
    </button>
  )
}
