import { useMemo } from 'react'
import type { ScoredEntry } from '../../api/types'
import { activeLocale } from '../../i18n/active'
import { collator, dec, useT } from '../../i18n'
import { DEFAULT_FILTERS, isDefault as filtersIsDefault, type LonglistFilters } from './filterState'

interface Props {
  scored: readonly ScoredEntry[]
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
}

export function FilterBar({ scored, filters, onChange }: Props) {
  const t = useT('history')

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
      .sort((a, b) => text.compare(a.label, b.label))
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
          {portals.map(({ slug, label, count }) => (
            <Chip key={slug} active={filters.portals.includes(slug)} onClick={() => togglePortal(slug)}>
              {label} <span className="chip__count">{count}</span>
            </Chip>
          ))}
        </ChipGroup>
      )}

      <PillGroup label={t.filterPosted}>
        {(['any', '24h', '7d', '14d', '30d'] as const).map((k) => (
          <Pill key={k} active={filters.posted === k} onClick={() => onChange({ ...filters, posted: k })}>
            {k === 'any' ? t.filterPostedAny : k}
          </Pill>
        ))}
      </PillGroup>

      <div className="longlist__score">
        <label className="muted small">{t.ratingRange(dec(filters.scoreMin, 2), dec(filters.scoreMax, 2))}</label>
        <input
          type="range" min={0} max={1} step={0.01}
          value={filters.scoreMin}
          onChange={(e) => onChange({ ...filters, scoreMin: clamp01(parseFloat(e.target.value)) })}
        />
        <input
          type="range" min={0} max={1} step={0.01}
          value={filters.scoreMax}
          onChange={(e) => onChange({ ...filters, scoreMax: clamp01(parseFloat(e.target.value)) })}
        />
      </div>

      {stackHits.length > 0 && (
        <ChipGroup label={t.filterSkillMatch}>
          {stackHits.map(([s, count]) => (
            <Chip key={s} active={filters.stackHits.includes(s)} onClick={() => toggleStack(s)}>
              {s} <span className="chip__count">{count}</span>
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

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }
