import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { getHistory, getRun } from '../api/client'
import { ListingCard } from '../components/ListingCard'
import { RunSummaryCard } from '../components/RunSummaryCard'
import { formatAbsolute, formatRelative } from '../utils/time'
import type {
  DropReason,
  DroppedEntry,
  RunDetail,
  ScoreBreakdown,
  ScoredEntry,
} from '../api/types'

function HistoryListView() {
  const navigate = useNavigate()
  const { data, isLoading, error } = useQuery({
    queryKey: ['history'],
    queryFn: getHistory,
  })

  return (
    <div className="page page--wide">
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
        <div className="table-wrap">
          <table className="table table--clickable">
            <thead>
              <tr>
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
                return (
                  <tr key={run.runId} onClick={() => navigate(`/history/${run.runId}`)}>
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
      )}
    </div>
  )
}

type TabKey = 'shortlist' | 'raw' | 'dedupe' | 'ranking' | 'dropped'

function parseHash(hash: string): { tab: TabKey; provider?: string } {
  const cleaned = hash.startsWith('#') ? hash.slice(1) : hash
  const params = new URLSearchParams(cleaned)
  const tab = params.get('tab')
  const provider = params.get('provider') ?? undefined
  const valid: TabKey[] = ['shortlist', 'raw', 'dedupe', 'ranking', 'dropped']
  if (tab && (valid as string[]).includes(tab)) return { tab: tab as TabKey, provider }
  return { tab: 'shortlist', provider }
}

function buildHash(tab: TabKey, provider?: string): string {
  const params = new URLSearchParams()
  params.set('tab', tab)
  if (provider) params.set('provider', provider)
  return `#${params.toString()}`
}

function TabStrip({
  active,
  onChange,
  data,
}: {
  active: TabKey
  onChange: (tab: TabKey) => void
  data: RunDetail
}) {
  const tabs: { key: TabKey; label: string; count?: number; available: boolean }[] = [
    { key: 'shortlist', label: 'Shortlist', count: data.shortlist.length, available: true },
    { key: 'raw',       label: 'Raw fetch', count: data.raw?.reduce((n, p) => n + p.listings.length, 0), available: !!data.raw },
    { key: 'dedupe',    label: 'Dedupe',    count: data.dedupeMerges?.length, available: !!data.dedupeMerges },
    { key: 'ranking',   label: 'Full ranking', count: data.scored?.length, available: !!data.scored },
    { key: 'dropped',   label: 'Dropped',   count: data.dropped?.length, available: !!data.dropped },
  ]
  return (
    <nav className="tabs" aria-label="Run sections">
      {tabs.map(t => (
        <button
          key={t.key}
          type="button"
          className={`tab ${active === t.key ? 'tab--active' : ''} ${!t.available ? 'tab--disabled' : ''}`}
          onClick={() => t.available && onChange(t.key)}
          disabled={!t.available}
          title={t.available ? '' : 'Not recorded for this run.'}
        >
          {t.label}
          {t.count !== undefined && <span className="tab__count">{t.count}</span>}
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
            className={`raw-group ${focusProvider === group.provider ? 'raw-group--focus' : ''}`}
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

const COMPONENT_LABELS: Array<{ key: keyof ScoreBreakdown; label: string }> = [
  { key: 'primaryStack', label: 'primary' },
  { key: 'secondaryStack', label: 'secondary' },
  { key: 'seniority', label: 'seniority' },
  { key: 'locationRemote', label: 'location' },
  { key: 'domain', label: 'domain' },
  { key: 'freshness', label: 'freshness' },
]

function BreakdownBar({ b }: { b: ScoreBreakdown }) {
  const positives = COMPONENT_LABELS.map(c => ({ ...c, value: Math.max(0, b[c.key]) }))
  const totalPositive = positives.reduce((n, c) => n + c.value, 0)
  if (totalPositive === 0 && b.disqualifierPenalty === 0) {
    return <span className="muted">—</span>
  }
  return (
    <div className="bd-bar" aria-label="score breakdown">
      {positives.map((c, i) => {
        if (c.value <= 0) return null
        const pct = (c.value / Math.max(totalPositive, 0.001)) * 100
        return (
          <span
            key={c.key}
            className={`bd-bar__seg bd-bar__seg--${i}`}
            style={{ width: `${pct}%` }}
            title={`${c.label}: ${c.value.toFixed(3)}`}
          />
        )
      })}
      {b.disqualifierPenalty < 0 && (
        <span
          className="bd-bar__seg bd-bar__seg--penalty"
          style={{ width: '100%' }}
          title={`disqualifier penalty: ${b.disqualifierPenalty.toFixed(3)}`}
        />
      )}
    </div>
  )
}

type RankingSort = 'score' | 'title' | 'company'

function RankingTab({ data }: { data: RunDetail }) {
  const [sort, setSort] = useState<RankingSort>('score')
  const [expanded, setExpanded] = useState<string | null>(null)

  const ordered = useMemo(() => {
    if (!data.scored) return []
    const arr = [...data.scored]
    if (sort === 'score') arr.sort((a, b) => b.score - a.score)
    else if (sort === 'title') arr.sort((a, b) => a.title.localeCompare(b.title))
    else arr.sort((a, b) => (a.company ?? '').localeCompare(b.company ?? ''))
    return arr
  }, [data.scored, sort])

  if (!data.scored) {
    return <div className="muted">No ranking data recorded for this run.</div>
  }

  return (
    <section>
      <div className="ranking-controls">
        <span className="muted">sort by</span>
        {(['score', 'title', 'company'] as RankingSort[]).map(s => (
          <button
            key={s}
            type="button"
            className={`chip ${sort === s ? 'chip--active' : ''}`}
            onClick={() => setSort(s)}
          >
            {s}
          </button>
        ))}
      </div>
      <div className="table-wrap">
        <table className="table ranking-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Company</th>
              <th>Score</th>
              <th>Breakdown</th>
            </tr>
          </thead>
          <tbody>
            {ordered.map(s => (
              <RankingRow
                key={s.id}
                entry={s}
                expanded={expanded === s.id}
                onToggle={() => setExpanded(expanded === s.id ? null : s.id)}
              />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function RankingRow({
  entry,
  expanded,
  onToggle,
}: {
  entry: ScoredEntry
  expanded: boolean
  onToggle: () => void
}) {
  return (
    <>
      <tr onClick={onToggle} style={{ cursor: 'pointer' }}>
        <td>
          <a href={entry.url} target="_blank" rel="noreferrer" onClick={e => e.stopPropagation()}>
            {entry.title}
          </a>
        </td>
        <td>{entry.company ?? <span className="muted">—</span>}</td>
        <td className="tabular mono">{entry.score.toFixed(2)}</td>
        <td><BreakdownBar b={entry.breakdown} /></td>
      </tr>
      {expanded && (
        <tr className="ranking-table__expanded">
          <td colSpan={4}>
            <div className="bd-detail">
              {COMPONENT_LABELS.map(c => (
                <div key={c.key} className="bd-detail__row">
                  <span className="bd-detail__label">{c.label}</span>
                  <span className="bd-detail__value mono tabular">
                    {entry.breakdown[c.key].toFixed(3)}
                  </span>
                </div>
              ))}
              {entry.breakdown.disqualifierPenalty !== 0 && (
                <div className="bd-detail__row">
                  <span className="bd-detail__label" style={{ color: 'var(--c-bad)' }}>disqualifier penalty</span>
                  <span className="bd-detail__value mono tabular" style={{ color: 'var(--c-bad)' }}>
                    {entry.breakdown.disqualifierPenalty.toFixed(3)}
                  </span>
                </div>
              )}
              <div className="bd-detail__row bd-detail__row--total">
                <span className="bd-detail__label">total (clamped)</span>
                <span className="bd-detail__value mono tabular">{entry.score.toFixed(3)}</span>
              </div>
            </div>
          </td>
        </tr>
      )}
    </>
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
          <TabStrip active={tab} onChange={setTab} data={data} />
          {tab === 'shortlist' && <ShortlistTab data={data} />}
          {tab === 'raw'       && <RawFetchTab data={data} focusProvider={provider} />}
          {tab === 'dedupe'    && <DedupeTab data={data} />}
          {tab === 'ranking'   && <RankingTab data={data} />}
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
