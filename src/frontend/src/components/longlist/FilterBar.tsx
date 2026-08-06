import { useMemo, useState } from 'react'
import type { ScoredEntry } from '../../api/types'
import { activeLocale } from '../../i18n/active'
import { collator, n, useT } from '../../i18n'
import { DEFAULT_FILTERS, isDefault as filtersIsDefault, type LonglistFilters } from './filterState'
import { ScoreRange } from './ScoreRange'

interface Props {
  scored: readonly ScoredEntry[]
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
}

const VISIBLE_SOURCES = 8

export function FilterBar({ scored, filters, onChange }: Props) {
  const t = useT('history')
  const [showAllSources, setShowAllSources] = useState(false)

  // Busiest source first, not alphabetical: a real run has ~50 sources whose long tail is mostly
  // single-digit, and only the leading handful is shown until asked. Alphabetical order would make
  // that truncation arbitrary — it put SimCorp's 259 listings between Saxo Bank and Solita.
  const portals = useMemo(() => {
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

  // A selected source stays visible even when it falls outside the leading slice — otherwise
  // collapsing hides an active filter and the row count has no visible explanation.
  const visiblePortals = showAllSources
    ? portals
    : portals.filter((p, i) => i < VISIBLE_SOURCES || filters.portals.includes(p.slug))

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

  return (
    <div className="longlist__filter-bar">
      <input
        className="input longlist__search"
        type="search"
        placeholder={t.searchTitleOrCompany}
        value={filters.q}
        onChange={(e) => onChange({ ...filters, q: e.target.value })}
        onKeyDown={(e) => { if (e.key === 'Escape') onChange({ ...filters, q: '' }) }}
      />

      {portals.length > 0 && (
        <ChipGroup label={t.filterSource}>
          {visiblePortals.map(({ slug, label, count }) => (
            <Chip key={slug} active={filters.portals.includes(slug)} onClick={() => togglePortal(slug)}>
              {label} <span className="chip__count">{n(count)}</span>
            </Chip>
          ))}
          {portals.length > VISIBLE_SOURCES && (
            <button type="button" className="link-button" onClick={() => setShowAllSources(!showAllSources)}>
              {showAllSources ? t.filterShowFewerSources : t.filterShowAllSources(portals.length)}
            </button>
          )}
        </ChipGroup>
      )}

      <PillGroup label={t.filterPosted}>
        {(['any', '24h', '7d', '14d', '30d'] as const).map((k) => (
          <Pill key={k} active={filters.posted === k} onClick={() => onChange({ ...filters, posted: k })}>
            {k === 'any' ? t.filterPostedAny : k}
          </Pill>
        ))}
      </PillGroup>

      <ScoreRange filters={filters} onChange={onChange} />

      {stackHits.length > 0 && (
        <ChipGroup label={t.filterSkillMatch}>
          {stackHits.map(([s, count]) => (
            <Chip key={s} active={filters.stackHits.includes(s)} onClick={() => toggleStack(s)}>
              {s} <span className="chip__count">{n(count)}</span>
            </Chip>
          ))}
        </ChipGroup>
      )}

      <PillGroup label={t.filterYourRating}>
        {(['all', 'good', 'bad', 'unmarked'] as const).map((k) => (
          <Pill key={k} active={filters.mark === k} onClick={() => onChange({ ...filters, mark: k })}>
            {k === 'all' ? t.markAll : k === 'good' ? t.markGood : k === 'bad' ? t.markBad : t.markUnmarked}
          </Pill>
        ))}
      </PillGroup>

      <label className="longlist__toggle">
        <input
          type="checkbox"
          checked={filters.shortlistOnly}
          onChange={(e) => onChange({ ...filters, shortlistOnly: e.target.checked })}
        />
        <span>{t.topJobsOnly}</span>
      </label>

      {!filtersIsDefault(filters) && (
        <button type="button" className="link-button" onClick={() => onChange(DEFAULT_FILTERS)}>
          {t.resetFilters}
        </button>
      )}
    </div>
  )
}

function ChipGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="longlist__chips" role="group" aria-label={label}>
      <span className="muted small">{label}:</span>
      {children}
    </div>
  )
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" className={`chip ${active ? 'chip--active' : ''}`} aria-pressed={active} onClick={onClick}>
      {children}
    </button>
  )
}

function PillGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="longlist__pills" role="group" aria-label={label}>
      <span className="muted small">{label}:</span>
      {children}
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
