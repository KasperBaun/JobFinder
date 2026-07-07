import { useMemo, useState } from 'react'
import type { ApplicationStatus, RunDetail, ScoredEntry } from '../api/types'
import {
  DEFAULT_FILTERS,
  isDefault as filtersIsDefault,
  type LonglistFilters,
  type SortKey,
} from './longlist/filterState'
import { BreakdownBar, BreakdownDetail } from './BreakdownBar'
import { MarkButton } from './MarkButton'
import { StatusSelect } from './StatusSelect'
import { formatRelative } from '../utils/time'

interface Props {
  data: RunDetail
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
  shortlistIds: Set<string>
}

export function LonglistTable({ data, filters, onChange, shortlistIds }: Props) {
  if (!data.scored) return <div className="muted">No ratings recorded for this search.</div>

  const portalCounts = useMemo(() => countBy(data.scored ?? [], (e) => e.portal), [data.scored])
  const portalDisplayNames = useMemo(() => {
    const m = new Map<string, string>()
    for (const e of data.scored ?? []) {
      if (!m.has(e.portal)) m.set(e.portal, e.portalDisplayName ?? e.portal)
    }
    return m
  }, [data.scored])
  const stackCounts = useMemo(() => countStackHits(data.scored ?? []), [data.scored])

  const filtered = useMemo(
    () => applyFilters(data.scored ?? [], filters, data.marks, shortlistIds),
    [data.scored, filters, data.marks, shortlistIds],
  )

  const setSort = (key: SortKey) => {
    if (filters.sort.key === key) {
      onChange({ ...filters, sort: { key, dir: filters.sort.dir === 'asc' ? 'desc' : 'asc' } })
    } else {
      onChange({ ...filters, sort: { key, dir: key === 'score' ? 'desc' : 'asc' } })
    }
  }

  return (
    <section className="longlist">
      <FilterBar
        filters={filters}
        onChange={onChange}
        portalCounts={portalCounts}
        portalDisplayNames={portalDisplayNames}
        stackCounts={stackCounts}
      />
      <div className="longlist__strip muted">
        {filtered.length} of {data.scored.length}
        {' · sorted by '}{sortKeyLabel(filters.sort.key)}{' '}{filters.sort.dir === 'desc' ? '↓' : '↑'}
      </div>
      <div className="table-wrap">
        <table className="table longlist__table">
          <thead>
            <tr>
              <SortableHeader sortKey="title" filters={filters} onClick={setSort}>Title</SortableHeader>
              <SortableHeader sortKey="company" filters={filters} onClick={setSort}>Company</SortableHeader>
              <SortableHeader sortKey="portal" filters={filters} onClick={setSort}>Source</SortableHeader>
              <SortableHeader sortKey="location" filters={filters} onClick={setSort}>Location</SortableHeader>
              <SortableHeader sortKey="posted" filters={filters} onClick={setSort}>Posted</SortableHeader>
              <SortableHeader sortKey="score" filters={filters} onClick={setSort}>Rating</SortableHeader>
              <th>Your rating</th>
              <th aria-label="expand"></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((s) => (
              <Row
                key={s.id}
                entry={s}
                runId={data.runId}
                mark={data.marks[s.id]}
                markReason={data.markReasons?.[s.id]}
                markStatus={data.markStatuses?.[s.id]}
              />
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && (
          <div className="muted longlist__empty">
            No jobs match these filters.{' '}
            <button type="button" className="link-button" onClick={() => onChange(DEFAULT_FILTERS)}>
              Reset
            </button>
          </div>
        )}
      </div>
    </section>
  )
}

function SortableHeader({
  sortKey,
  filters,
  onClick,
  children,
}: {
  sortKey: SortKey
  filters: LonglistFilters
  onClick: (key: SortKey) => void
  children: React.ReactNode
}) {
  const active = filters.sort.key === sortKey
  return (
    <th
      className={`sortable ${active ? 'sortable--active' : ''}`}
      onClick={() => onClick(sortKey)}
      style={{ cursor: 'pointer', userSelect: 'none' }}
    >
      {children}
      {active && <span aria-hidden> {filters.sort.dir === 'desc' ? '↓' : '↑'}</span>}
    </th>
  )
}

function FilterBar({
  filters, onChange, portalCounts, portalDisplayNames, stackCounts,
}: {
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
  portalCounts: Map<string, number>
  portalDisplayNames: Map<string, string>
  stackCounts: Map<string, number>
}) {
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

  const isDefault = filtersIsDefault(filters)

  return (
    <div className="longlist__filter-bar">
      <input
        className="input longlist__search"
        type="search"
        placeholder="Search title or company…"
        value={filters.q}
        onChange={(e) => onChange({ ...filters, q: e.target.value })}
        onKeyDown={(e) => { if (e.key === 'Escape') onChange({ ...filters, q: '' }) }}
      />

      {portalCounts.size > 0 && (
        <ChipGroup label="source">
          {[...portalCounts]
            .map(([p, n]) => ({ slug: p, label: portalDisplayNames.get(p) ?? p, count: n }))
            .sort((a, b) => a.label.localeCompare(b.label))
            .map(({ slug, label, count }) => (
              <Chip key={slug} active={filters.portals.includes(slug)} onClick={() => togglePortal(slug)}>
                {label} <span className="chip__count">{count}</span>
              </Chip>
            ))}
        </ChipGroup>
      )}

      <PillGroup label="posted">
        {(['any', '24h', '7d', '14d', '30d'] as const).map((k) => (
          <Pill key={k} active={filters.posted === k} onClick={() => onChange({ ...filters, posted: k })}>
            {k}
          </Pill>
        ))}
      </PillGroup>

      <div className="longlist__score">
        <label className="muted small">rating {filters.scoreMin.toFixed(2)}–{filters.scoreMax.toFixed(2)}</label>
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

      {stackCounts.size > 0 && (
        <ChipGroup label="skill match">
          {[...stackCounts].sort(([, a], [, b]) => b - a).map(([s, n]) => (
            <Chip key={s} active={filters.stackHits.includes(s)} onClick={() => toggleStack(s)}>
              {s} <span className="chip__count">{n}</span>
            </Chip>
          ))}
        </ChipGroup>
      )}

      <PillGroup label="your rating">
        {(['all', 'good', 'bad', 'unmarked'] as const).map((k) => (
          <Pill key={k} active={filters.mark === k} onClick={() => onChange({ ...filters, mark: k })}>
            {k === 'unmarked' ? 'not rated' : k}
          </Pill>
        ))}
      </PillGroup>

      <label className="longlist__toggle">
        <input
          type="checkbox"
          checked={filters.shortlistOnly}
          onChange={(e) => onChange({ ...filters, shortlistOnly: e.target.checked })}
        />
        <span>top jobs only</span>
      </label>

      {!isDefault && (
        <button type="button" className="link-button" onClick={() => onChange(DEFAULT_FILTERS)}>
          Reset filters
        </button>
      )}
    </div>
  )
}

function ChipGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="longlist__chips" aria-label={label}>
      <span className="muted small">{label}:</span>
      {children}
    </div>
  )
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      className={`chip ${active ? 'chip--active' : ''}`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}

function PillGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="longlist__pills" aria-label={label}>
      <span className="muted small">{label}:</span>
      {children}
    </div>
  )
}

function Pill({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      className={`pill ${active ? 'pill--active' : ''}`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}

function Row({ entry, runId, mark, markReason, markStatus }: {
  entry: ScoredEntry
  runId: string
  mark?: 'good' | 'bad'
  markReason?: string
  markStatus?: ApplicationStatus
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <tr>
        <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
        <td>{entry.company ?? <span className="muted">—</span>}</td>
        <td><span className="badge badge--muted">{entry.portalDisplayName ?? entry.portal}</span></td>
        <td>{entry.location ?? <span className="muted">—</span>}</td>
        <td className="tabular mono">
          {entry.postedAt ? formatRelative(entry.postedAt) : <span className="muted">—</span>}
        </td>
        <td className="tabular mono">
          <div className="longlist__score-cell">
            <span>{entry.score.toFixed(2)}</span>
            <BreakdownBar b={entry.breakdown} />
          </div>
        </td>
        <td>
          <div className="longlist__mark-cell">
            <MarkButton runId={runId} listingId={entry.id} current={mark} reason={markReason} compact />
            <StatusSelect runId={runId} listingId={entry.id} current={markStatus} compact />
          </div>
        </td>
        <td>
          <button type="button" className="link-button" onClick={() => setOpen(!open)} aria-label={open ? 'collapse' : 'expand'}>
            {open ? '▾' : '▸'}
          </button>
        </td>
      </tr>
      {open && (
        <tr className="longlist__expanded">
          <td colSpan={8}>
            <BreakdownDetail entry={entry} />
          </td>
        </tr>
      )}
    </>
  )
}

function sortKeyLabel(key: SortKey): string {
  switch (key) {
    case 'portal': return 'source'
    case 'score':  return 'rating'
    default:       return key
  }
}

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }

function applyFilters(
  rows: readonly ScoredEntry[],
  f: LonglistFilters,
  marks: Record<string, 'good' | 'bad'>,
  shortlistIds: Set<string>,
): ScoredEntry[] {
  const q = f.q.trim().toLowerCase()
  const portals = new Set(f.portals)
  const stack = new Set(f.stackHits.map((s) => s.toLowerCase()))
  const cutoff = postedCutoff(f.posted)

  const filtered = rows.filter((r) => {
    if (q && !(`${r.title} ${r.company ?? ''}`.toLowerCase().includes(q))) return false
    if (portals.size && !portals.has(r.portal)) return false
    if (cutoff && (!r.postedAt || new Date(r.postedAt).getTime() < cutoff)) return false
    if (r.score < f.scoreMin || r.score > f.scoreMax) return false
    if (stack.size) {
      const hits = [...r.primaryStackHits, ...r.secondaryStackHits].map((s) => s.toLowerCase())
      if (!hits.some((h) => stack.has(h))) return false
    }
    if (f.mark !== 'all') {
      const m = marks[r.id]
      if (f.mark === 'unmarked' ? m !== undefined : m !== f.mark) return false
    }
    if (f.shortlistOnly && !shortlistIds.has(r.id)) return false
    return true
  })

  const cmp = sortComparator(f.sort.key, f.sort.dir)
  return [...filtered].sort(cmp)
}

function postedCutoff(p: LonglistFilters['posted']): number | null {
  if (p === 'any') return null
  const days = p === '24h' ? 1 : p === '7d' ? 7 : p === '14d' ? 14 : 30
  return Date.now() - days * 86_400_000
}

function sortComparator(key: SortKey, dir: 'asc' | 'desc') {
  const sign = dir === 'asc' ? 1 : -1
  return (a: ScoredEntry, b: ScoredEntry) => {
    let v = 0
    switch (key) {
      case 'title':    v = a.title.localeCompare(b.title); break
      case 'company':  v = (a.company ?? '').localeCompare(b.company ?? ''); break
      case 'portal':   v = (a.portalDisplayName ?? a.portal).localeCompare(b.portalDisplayName ?? b.portal); break
      case 'location': v = (a.location ?? '').localeCompare(b.location ?? ''); break
      case 'posted':   v = (a.postedAt ?? '').localeCompare(b.postedAt ?? ''); break
      case 'score':    v = a.score - b.score; break
    }
    return sign * v
  }
}

function countBy<T, K extends string>(rows: readonly T[], key: (row: T) => K): Map<K, number> {
  const m = new Map<K, number>()
  for (const r of rows) {
    const k = key(r)
    m.set(k, (m.get(k) ?? 0) + 1)
  }
  return m
}

function countStackHits(rows: readonly ScoredEntry[]): Map<string, number> {
  const m = new Map<string, number>()
  for (const r of rows) {
    for (const h of [...r.primaryStackHits, ...r.secondaryStackHits]) {
      m.set(h, (m.get(h) ?? 0) + 1)
    }
  }
  return m
}
