import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { deleteHistoryRuns, getHistory, getRun } from '../api/client'
import { ListingCard } from '../components/ListingCard'
import { RunSummaryCard } from '../components/RunSummaryCard'
import { Toast } from '../components/Toast'
import { LonglistTable } from '../components/LonglistTable'
import {
  decodeFromHash,
  encodeToHash,
  type LonglistFilters,
} from '../components/longlist/filterState'
import { formatAbsolute, formatRelative } from '../utils/time'
import type {
  DropReason,
  DroppedEntry,
  RunDetail,
} from '../api/types'

function HistoryListView() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({
    queryKey: ['history'],
    queryFn: getHistory,
  })

  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const headerCheckboxRef = useRef<HTMLInputElement>(null)

  const visibleIds = useMemo(() => data?.runs.map(r => r.runId) ?? [], [data])
  const allSelected = visibleIds.length > 0 && visibleIds.every(id => selected.has(id))
  const someSelected = !allSelected && visibleIds.some(id => selected.has(id))

  useEffect(() => {
    if (headerCheckboxRef.current) {
      headerCheckboxRef.current.indeterminate = someSelected
    }
  }, [someSelected])

  useEffect(() => {
    if (!data) return
    setSelected(prev => {
      const valid = new Set(visibleIds)
      let changed = false
      const next = new Set<string>()
      for (const id of prev) {
        if (valid.has(id)) next.add(id)
        else changed = true
      }
      return changed ? next : prev
    })
  }, [data, visibleIds])

  const deleteMutation = useMutation({
    mutationFn: (runIds: string[]) => deleteHistoryRuns(runIds),
    onSuccess: (res) => {
      if (res.error) {
        setToast({ kind: 'err', message: res.error })
        return
      }
      setSelected(new Set())
      void queryClient.invalidateQueries({ queryKey: ['history'] })
      const skipped = res.missing.length
      const msg = skipped > 0
        ? `Deleted ${res.deleted} run${res.deleted === 1 ? '' : 's'} (${skipped} skipped)`
        : `Deleted ${res.deleted} run${res.deleted === 1 ? '' : 's'}`
      setToast({ kind: 'ok', message: msg })
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function toggleRow(id: string) {
    setSelected(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleAll() {
    if (allSelected) setSelected(new Set())
    else setSelected(new Set(visibleIds))
  }

  function onDeleteClick() {
    if (selected.size === 0) return
    const count = selected.size
    const ok = window.confirm(
      count === 1
        ? 'Delete this run? Its marks will also be removed. This cannot be undone.'
        : `Delete ${count} runs? Their marks will also be removed. This cannot be undone.`,
    )
    if (!ok) return
    deleteMutation.mutate(Array.from(selected))
  }

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">04 / history</div>
        <h1 className="page__heading">Past <em>runs</em></h1>
        <p className="page__lede">Every search you've run, with the shortlist and your marks preserved.</p>
      </header>

      {isLoading && <div className="muted">Loading history…</div>}
      {error && <div className="error-text">Failed to load history.</div>}

      {data && data.runs.length === 0 && (
        <div className="hint-card">No runs yet. Start one from the <Link to="/search">Search</Link> page.</div>
      )}

      {data && data.runs.length > 0 && (
        <>
          {selected.size > 0 && (
            <div className="selection-bar" role="region" aria-label="Selection actions">
              <span className="selection-bar__count">{selected.size} selected</span>
              <button
                type="button"
                className="btn btn--small"
                onClick={() => setSelected(new Set())}
                disabled={deleteMutation.isPending}
              >
                Clear
              </button>
              <button
                type="button"
                className="btn btn--small btn--danger"
                onClick={onDeleteClick}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Deleting…' : 'Delete selected'}
              </button>
            </div>
          )}

          <div className="table-wrap">
            <table className="table table--clickable">
              <thead>
                <tr>
                  <th className="table__select-cell">
                    <input
                      ref={headerCheckboxRef}
                      type="checkbox"
                      aria-label={allSelected ? 'Deselect all runs' : 'Select all runs'}
                      checked={allSelected}
                      onChange={toggleAll}
                    />
                  </th>
                  <th>Started</th>
                  <th>Providers</th>
                  <th>Shortlist</th>
                  <th>Top score</th>
                  <th>Good marks</th>
                </tr>
              </thead>
              <tbody>
                {data.runs.map(run => {
                  const ok = run.providers.filter(p => p.status === 'ok').length
                  const failed = run.providers.filter(p => p.status === 'failed').length
                  const ratio = run.shortlistCount > 0 ? run.goodMarks / run.shortlistCount : 0
                  const isSelected = selected.has(run.runId)
                  return (
                    <tr
                      key={run.runId}
                      className={isSelected ? 'table__row--selected' : undefined}
                      onClick={() => navigate(`/history/${run.runId}`)}
                    >
                      <td
                        className="table__select-cell"
                        onClick={e => { e.stopPropagation(); toggleRow(run.runId) }}
                      >
                        <input
                          type="checkbox"
                          aria-label={`Select run from ${formatAbsolute(run.startedAt)}`}
                          checked={isSelected}
                          onChange={() => toggleRow(run.runId)}
                          onClick={e => e.stopPropagation()}
                        />
                      </td>
                      <td title={formatAbsolute(run.startedAt)}>
                        <Link to={`/history/${run.runId}`} onClick={e => e.stopPropagation()}>
                          {formatRelative(run.startedAt)}
                        </Link>
                      </td>
                      <td className="tabular">
                        <span style={{ color: 'var(--c-good)' }}>{ok}</span>
                        <span className="subtle"> / </span>
                        <span style={{ color: failed ? 'var(--c-bad)' : 'var(--c-text-subtle)' }}>{failed}</span>
                      </td>
                      <td className="tabular">{run.shortlistCount}</td>
                      <td className="tabular mono">{run.topScore.toFixed(2)}</td>
                      <td>
                        <div className="marks-cell">
                          <span>{run.goodMarks} / {run.shortlistCount}</span>
                          <div className="progress-bar" aria-hidden="true">
                            <div
                              className="progress-bar__fill"
                              style={{ width: `${Math.round(ratio * 100)}%` }}
                            />
                          </div>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}

type TabKey = 'shortlist' | 'longlist' | 'raw' | 'dedupe' | 'dropped'

function parseHash(hash: string): { tab: TabKey; provider?: string } {
  const cleaned = hash.startsWith('#') ? hash.slice(1) : hash
  const params = new URLSearchParams(cleaned)
  const raw = params.get('tab')
  const provider = params.get('provider') ?? undefined
  // Back-compat: legacy ?tab=ranking → longlist
  const normalised = raw === 'ranking' ? 'longlist' : raw
  const valid: TabKey[] = ['shortlist', 'longlist', 'raw', 'dedupe', 'dropped']
  if (normalised && (valid as string[]).includes(normalised)) {
    return { tab: normalised as TabKey, provider }
  }
  return { tab: 'shortlist', provider }
}

function buildHash(tab: TabKey, provider?: string): string {
  const params = new URLSearchParams()
  params.set('tab', tab)
  if (provider) params.set('provider', provider)
  return `#${params.toString()}`
}

function ResultsToggle({
  active,
  onChange,
  data,
}: {
  active: TabKey
  onChange: (tab: TabKey) => void
  data: RunDetail
}) {
  const longlistCount = data.scored?.length
  const longlistAvailable = !!data.scored
  return (
    <div className="view-toggle" role="tablist" aria-label="Result view">
      <button
        type="button"
        role="tab"
        aria-selected={active === 'shortlist'}
        className={`view-toggle__seg ${active === 'shortlist' ? 'view-toggle__seg--active' : ''}`}
        onClick={() => onChange('shortlist')}
      >
        Shortlist <span className="view-toggle__count">{data.shortlist.length}</span>
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={active === 'longlist'}
        className={`view-toggle__seg ${active === 'longlist' ? 'view-toggle__seg--active' : ''}`}
        onClick={() => longlistAvailable && onChange('longlist')}
        disabled={!longlistAvailable}
        title={longlistAvailable ? 'All ranked candidates, including those beyond top-N' : 'Not recorded for this run.'}
      >
        Longlist {longlistCount !== undefined && <span className="view-toggle__count">{longlistCount}</span>}
      </button>
    </div>
  )
}

function AuditTabs({
  active,
  onChange,
  data,
}: {
  active: TabKey
  onChange: (tab: TabKey) => void
  data: RunDetail
}) {
  const tabs: { key: TabKey; label: string; count?: number; available: boolean }[] = [
    { key: 'raw',     label: 'raw fetch', count: data.raw?.reduce((n, p) => n + p.listings.length, 0), available: !!data.raw },
    { key: 'dedupe',  label: 'dedupe',    count: data.dedupeMerges?.length, available: !!data.dedupeMerges },
    { key: 'dropped', label: 'dropped',   count: data.dropped?.length, available: !!data.dropped },
  ]
  return (
    <nav className="audit-tabs" aria-label="Audit views">
      <span className="audit-tabs__label">audit:</span>
      {tabs.map(t => (
        <button
          key={t.key}
          type="button"
          className={`audit-tab ${active === t.key ? 'audit-tab--active' : ''} ${!t.available ? 'audit-tab--disabled' : ''}`}
          onClick={() => t.available && onChange(t.key)}
          disabled={!t.available}
          title={t.available ? '' : 'Not recorded for this run.'}
        >
          {t.label}
          {t.count !== undefined && <span className="audit-tab__count">{t.count}</span>}
        </button>
      ))}
    </nav>
  )
}

function ShortlistTab({ data }: { data: RunDetail }) {
  return (
    <section className="results">
      <h2 className="results__heading">
        Shortlist <span className="muted serif" style={{ fontStyle: 'italic' }}>({data.shortlist.length})</span>
      </h2>
      {data.shortlist.length === 0 && <div className="muted">No listings on this run's shortlist.</div>}
      <div className="listing-list">
        {data.shortlist.map(m => (
          <ListingCard
            key={m.id}
            match={m}
            runId={data.runId}
            mark={data.marks[m.id]}
          />
        ))}
      </div>
    </section>
  )
}

function RawFetchTab({ data, focusProvider }: { data: RunDetail; focusProvider?: string }) {
  const [open, setOpen] = useState<Set<string>>(() =>
    new Set(focusProvider ? [focusProvider] : data.raw?.map(p => p.provider) ?? [])
  )

  useEffect(() => {
    if (!focusProvider) return
    const el = document.getElementById(`raw-${focusProvider}`)
    if (!el) return
    const t = window.setTimeout(
      () => el.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      50,
    )
    return () => window.clearTimeout(t)
  }, [focusProvider])

  if (!data.raw) {
    return <div className="muted">No raw fetch data recorded for this run.</div>
  }
  return (
    <section className="raw-fetch">
      {data.raw.map(group => {
        const isOpen = open.has(group.provider)
        return (
          <div
            key={group.provider}
            id={`raw-${group.provider}`}
            className={[
              'raw-group',
              isOpen ? 'raw-group--open' : '',
              focusProvider === group.provider ? 'raw-group--focus' : '',
            ].filter(Boolean).join(' ')}
          >
            <button
              type="button"
              className="raw-group__header"
              onClick={() => {
                const next = new Set(open)
                if (isOpen) next.delete(group.provider)
                else next.add(group.provider)
                setOpen(next)
              }}
            >
              <span className="raw-group__caret" aria-hidden="true">{isOpen ? '▾' : '▸'}</span>
              <span className="raw-group__name">{group.provider}</span>
              <span className="raw-group__count">{group.listings.length}</span>
            </button>
            {isOpen && group.listings.length > 0 && (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Title</th>
                      <th>Company</th>
                      <th>Location</th>
                      <th>Posted</th>
                      <th>Url</th>
                    </tr>
                  </thead>
                  <tbody>
                    {group.listings.map(l => (
                      <tr key={l.id}>
                        <td>{l.title}</td>
                        <td>{l.company ?? <span className="muted">—</span>}</td>
                        <td>{l.location ?? <span className="muted">—</span>}</td>
                        <td className="tabular mono">
                          {l.postedAt ? formatRelative(l.postedAt) : <span className="muted">—</span>}
                        </td>
                        <td><a href={l.url} target="_blank" rel="noreferrer">open ↗</a></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {isOpen && group.listings.length === 0 && (
              <div className="muted" style={{ padding: '0.5rem 1rem' }}>0 listings.</div>
            )}
          </div>
        )
      })}
    </section>
  )
}

function DedupeTab({ data }: { data: RunDetail }) {
  if (!data.dedupeMerges) {
    return <div className="muted">No dedupe data recorded for this run.</div>
  }
  if (data.dedupeMerges.length === 0) {
    return <div className="muted">No duplicates merged this run.</div>
  }
  // Build a lookup so we can show titles for canonical / merged listings.
  const titleById = new Map<string, string>()
  for (const r of data.raw ?? []) for (const l of r.listings) titleById.set(l.id, l.title)
  for (const s of data.scored ?? []) titleById.set(s.id, s.title)

  return (
    <section className="dedupe-list">
      {data.dedupeMerges.map(g => (
        <div key={g.canonicalId} className="dedupe-group">
          <div className="dedupe-group__canonical">
            <span className="dedupe-group__label">canonical</span>
            <span className="dedupe-group__title">{titleById.get(g.canonicalId) ?? g.canonicalId}</span>
          </div>
          <div className="dedupe-group__merges">
            <span className="dedupe-group__label">merged from {g.mergedFromIds.length}</span>
            <ul>
              {g.mergedFromIds.map(id => (
                <li key={id}>
                  {titleById.get(id) ?? <code className="mono">{id.slice(0, 12)}…</code>}
                </li>
              ))}
            </ul>
          </div>
        </div>
      ))}
    </section>
  )
}


const REASON_LABELS: Record<DropReason, string> = {
  disqualifier: 'disqualifier',
  below_min_score: 'below min score',
  beyond_top_n: 'beyond top-N',
  above_max_age: 'above max age',
  missing_required_primary: 'no primary hit',
}

function DroppedTab({ data }: { data: RunDetail }) {
  const [filter, setFilter] = useState<DropReason | 'all'>('all')
  if (!data.dropped) {
    return <div className="muted">No dropped data recorded for this run.</div>
  }
  if (data.dropped.length === 0) {
    return <div className="muted">Nothing was dropped this run.</div>
  }

  const counts = data.dropped.reduce<Record<string, number>>((acc, d) => {
    acc[d.reason] = (acc[d.reason] ?? 0) + 1
    return acc
  }, {})

  const filtered = filter === 'all' ? data.dropped : data.dropped.filter(d => d.reason === filter)

  return (
    <section>
      <div className="dropped-filters">
        <button
          type="button"
          className={`chip ${filter === 'all' ? 'chip--active' : ''}`}
          onClick={() => setFilter('all')}
        >
          all <span className="tab__count">{data.dropped.length}</span>
        </button>
        {(Object.keys(REASON_LABELS) as DropReason[]).map(r => (
          counts[r] ? (
            <button
              key={r}
              type="button"
              className={`chip ${filter === r ? 'chip--active' : ''}`}
              onClick={() => setFilter(r)}
            >
              {REASON_LABELS[r]} <span className="tab__count">{counts[r]}</span>
            </button>
          ) : null
        ))}
      </div>
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Company</th>
              <th>Score</th>
              <th>Reason</th>
              <th>Why</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(d => <DroppedRow key={d.id} entry={d} />)}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function DroppedRow({ entry }: { entry: DroppedEntry }) {
  return (
    <tr>
      <td>{entry.title}</td>
      <td>{entry.company ?? <span className="muted">—</span>}</td>
      <td className="tabular mono">{entry.score.toFixed(2)}</td>
      <td><span className={`reason-badge reason-badge--${entry.reason}`}>{REASON_LABELS[entry.reason]}</span></td>
      <td className="muted">{entry.context}</td>
    </tr>
  )
}

function LonglistView({ data }: { data: RunDetail }) {
  const navigate = useNavigate()
  const location = useLocation()

  const filters = useMemo(() => {
    const params = new URLSearchParams(location.hash.startsWith('#') ? location.hash.slice(1) : location.hash)
    return decodeFromHash(params)
  }, [location.hash])

  const shortlistIds = useMemo(() => new Set(data.shortlist.map((m) => m.id)), [data.shortlist])

  const setFilters = (next: LonglistFilters) => {
    const params = encodeToHash(next)
    navigate(`${location.pathname}#${params.toString()}`, { replace: true })
  }

  return (
    <LonglistTable
      data={data}
      filters={filters}
      onChange={setFilters}
      shortlistIds={shortlistIds}
    />
  )
}

function RunDetailView({ runId }: { runId: string }) {
  const navigate = useNavigate()
  const location = useLocation()
  const { tab, provider } = useMemo(() => parseHash(location.hash), [location.hash])

  const { data, isLoading, error } = useQuery({
    queryKey: ['run', runId],
    queryFn: () => getRun(runId),
  })

  function setTab(next: TabKey) {
    navigate(`/history/${runId}${buildHash(next)}`, { replace: false })
  }

  return (
    <div className="page page--wide">
      <header className="page__header">
        <Link to="/history" className="back-link">← back to history</Link>
        <div className="page__eyebrow">04 / history / detail</div>
        <h1 className="page__heading">Run <em>detail</em></h1>
      </header>

      {isLoading && <div className="muted">Loading run…</div>}
      {error && <div className="error-text">Failed to load run.</div>}

      {data && (
        <>
          <RunSummaryCard run={data} />
          <ResultsToggle active={tab} onChange={setTab} data={data} />
          <AuditTabs active={tab} onChange={setTab} data={data} />
          {tab === 'shortlist' && <ShortlistTab data={data} />}
          {tab === 'longlist'  && <LonglistView data={data} />}
          {tab === 'raw'       && <RawFetchTab data={data} focusProvider={provider} />}
          {tab === 'dedupe'    && <DedupeTab data={data} />}
          {tab === 'dropped'   && <DroppedTab data={data} />}
        </>
      )}
    </div>
  )
}

export function HistoryPage() {
  const { runId } = useParams<{ runId: string }>()
  if (runId) return <RunDetailView runId={runId} />
  return <HistoryListView />
}
