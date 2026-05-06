import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getHistory, getProviders } from '../api/client'
import { useSearchStream } from '../hooks/useSearchStream'
import { ListingCard } from '../components/ListingCard'
import { formatRelative } from '../utils/time'
import type { SearchProgressEvent, SearchRequest } from '../api/types'

type ProviderRowState = {
  name: string
  status: 'pending' | 'running' | 'ok' | 'failed'
  fetchedCount?: number
  error?: string
}

function reduceProgress(events: SearchProgressEvent[], initialNames: string[]): {
  rows: ProviderRowState[]
  dedupe?: number
  rank?: { rankedCount: number; topScore: number }
} {
  const rows = new Map<string, ProviderRowState>()
  for (const name of initialNames) rows.set(name, { name, status: 'pending' })

  let dedupe: number | undefined
  let rank: { rankedCount: number; topScore: number } | undefined

  for (const ev of events) {
    if (ev.type === 'provider_running') {
      rows.set(ev.provider, { name: ev.provider, status: 'running' })
    } else if (ev.type === 'provider_done') {
      rows.set(ev.provider, { name: ev.provider, status: 'ok', fetchedCount: ev.fetchedCount })
    } else if (ev.type === 'provider_failed') {
      rows.set(ev.provider, { name: ev.provider, status: 'failed', error: ev.error })
    } else if (ev.type === 'dedupe') {
      dedupe = ev.mergedCount
    } else if (ev.type === 'rank') {
      rank = { rankedCount: ev.rankedCount, topScore: ev.topScore }
    }
  }
  return { rows: Array.from(rows.values()), dedupe, rank }
}

export function SearchPage() {
  const providersQuery = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const historyQuery = useQuery({ queryKey: ['history'], queryFn: getHistory })
  const stream = useSearchStream()
  const lastRun = historyQuery.data?.runs[0]
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [topN, setTopN] = useState<string>('')
  const [minScore, setMinScore] = useState<string>('')
  const [selectedProviders, setSelectedProviders] = useState<string[] | null>(null)

  const enabledProviderNames = useMemo(
    () => providersQuery.data?.providers.filter(p => p.enabled).map(p => p.name) ?? [],
    [providersQuery.data],
  )

  const effectiveProviders = selectedProviders ?? enabledProviderNames

  const progress = useMemo(
    () => reduceProgress(stream.events, effectiveProviders),
    [stream.events, effectiveProviders],
  )

  const [progressExpanded, setProgressExpanded] = useState(true)
  useEffect(() => {
    if (stream.status === 'running') setProgressExpanded(true)
    if (stream.status === 'complete') {
      const t = setTimeout(() => setProgressExpanded(false), 350)
      return () => clearTimeout(t)
    }
  }, [stream.status])

  function toggleProvider(name: string) {
    const base = selectedProviders ?? enabledProviderNames
    const next = base.includes(name) ? base.filter(n => n !== name) : [...base, name]
    setSelectedProviders(next)
  }

  function handleRun() {
    const req: SearchRequest = {}
    if (selectedProviders !== null) req.providers = selectedProviders
    const tn = topN.trim() ? Number(topN) : NaN
    if (Number.isFinite(tn)) req.topN = tn
    const ms = minScore.trim() ? Number(minScore) : NaN
    if (Number.isFinite(ms)) req.minScore = ms
    stream.start(req)
  }

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">03 / search</div>
        <h1 className="page__heading">Run a <em>search</em></h1>
        <p className="page__lede">
          Fetches the latest listings from your enabled providers, dedupes, ranks against your skillset,
          and writes a shortlist to <code>top-jobs.md</code>.
        </p>
      </header>

      <div className="search-controls">
        <button
          type="button"
          className="btn btn--primary btn--lg"
          onClick={handleRun}
          disabled={stream.status === 'running'}
        >
          {stream.status === 'running' ? 'Running…' : 'Run a search'}
        </button>
        {stream.status !== 'idle' && stream.status !== 'running' && (
          <button type="button" className="btn btn--secondary" onClick={stream.reset}>
            Reset
          </button>
        )}
        <button
          type="button"
          className="link-button"
          onClick={() => setAdvancedOpen(o => !o)}
        >
          {advancedOpen ? 'Hide advanced' : 'Advanced…'}
        </button>
      </div>

      {advancedOpen && (
        <section className="card advanced-panel">
          <div className="field-grid">
            <div className="field">
              <label className="field__label" htmlFor="topN">Top N</label>
              <input
                id="topN"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder="from ranking.yml"
                value={topN}
                onChange={e => setTopN(e.target.value)}
                min={1}
              />
            </div>
            <div className="field">
              <label className="field__label" htmlFor="minScore">Min score</label>
              <input
                id="minScore"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder="from ranking.yml"
                value={minScore}
                onChange={e => setMinScore(e.target.value)}
                min={0}
                max={1}
                step={0.01}
              />
            </div>
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label">Providers</label>
              <div className="chip-group">
                {providersQuery.data?.providers.map(p => {
                  const active = effectiveProviders.includes(p.name)
                  return (
                    <button
                      key={p.name}
                      type="button"
                      className={active ? 'chip chip--active' : 'chip'}
                      onClick={() => toggleProvider(p.name)}
                      disabled={!p.enabled && !active}
                      title={p.enabled ? '' : 'Disabled in portals.yml'}
                    >
                      {p.name}
                    </button>
                  )
                })}
              </div>
            </div>
          </div>
        </section>
      )}

      {stream.status === 'idle' && (
        <div className="hint-card">
          {lastRun ? (
            <>
              Last run was <strong>{formatRelative(lastRun.startedAt)}</strong> —{' '}
              <span className="tabular">{lastRun.shortlistCount}</span> shortlisted, top score{' '}
              <span className="tabular mono">{lastRun.topScore.toFixed(2)}</span>.
              <br />
              Click <strong>Run a search</strong> for a fresh batch, or{' '}
              <Link to={`/history/${lastRun.runId}`}>view that run</Link>.
            </>
          ) : (
            <>Ready when you are. Click <strong>Run a search</strong> to fetch listings from your enabled providers.</>
          )}
        </div>
      )}

      {stream.status !== 'idle' && (
        <section className={`progress-panel${stream.status === 'complete' && !progressExpanded ? ' progress-panel--collapsed' : ''}`}>
          {stream.status === 'complete' ? (
            <button
              type="button"
              className="progress-summary"
              onClick={() => setProgressExpanded(e => !e)}
              aria-expanded={progressExpanded}
            >
              <span className="progress-summary__check" aria-hidden="true">✓</span>
              <span className="progress-summary__text">
                <span className="tabular">{progress.rows.filter(r => r.status === 'ok').length}</span>
                {' / '}
                <span className="tabular">{progress.rows.length}</span> providers
                {progress.dedupe !== undefined && <> <span className="progress-summary__sep">·</span> <span className="tabular">{progress.dedupe}</span> deduped</>}
                {progress.rank && <> <span className="progress-summary__sep">·</span> <span className="tabular">{progress.rank.rankedCount}</span> ranked <span className="progress-summary__sep">·</span> top <span className="tabular mono">{progress.rank.topScore.toFixed(2)}</span></>}
              </span>
              <span className="progress-summary__toggle">
                {progressExpanded ? 'hide steps' : 'show steps'} {progressExpanded ? '▴' : '▾'}
              </span>
            </button>
          ) : (
            <h2 className="progress-panel__heading">Progress</h2>
          )}
          <div className="progress-collapse"><div>
          <ul className="progress-list">
            {progress.rows.map(row => {
              const linkable = stream.status === 'complete' && !!stream.runId && row.status === 'ok'
              const body = (
                <>
                  <span className="progress-row__icon"><span className={`dot dot--${row.status}`} /></span>
                  <span className="progress-row__name">{row.name}</span>
                  <span className="progress-row__status">
                    {row.status === 'pending' && 'pending'}
                    {row.status === 'running' && 'running…'}
                    {row.status === 'ok' && `ok · ${row.fetchedCount ?? 0} jobs`}
                    {row.status === 'failed' && `failed: ${row.error ?? 'unknown error'}`}
                  </span>
                </>
              )
              const className = `progress-row progress-row--${row.status}${linkable ? ' progress-row--link' : ''}`
              return (
                <li key={row.name} className={className}>
                  {linkable ? (
                    <Link to={`/history/${stream.runId}#tab=raw&provider=${encodeURIComponent(row.name)}`}>
                      {body}
                    </Link>
                  ) : body}
                </li>
              )
            })}
            {progress.dedupe !== undefined && (
              <li className={`progress-row progress-row--info${stream.status === 'complete' && stream.runId ? ' progress-row--link' : ''}`}>
                {stream.status === 'complete' && stream.runId ? (
                  <Link to={`/history/${stream.runId}#tab=dedupe`}>
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">dedupe</span>
                    <span className="progress-row__status">{progress.dedupe} after merge</span>
                  </Link>
                ) : (
                  <>
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">dedupe</span>
                    <span className="progress-row__status">{progress.dedupe} after merge</span>
                  </>
                )}
              </li>
            )}
            {progress.rank !== undefined && (
              <li className={`progress-row progress-row--info${stream.status === 'complete' && stream.runId ? ' progress-row--link' : ''}`}>
                {stream.status === 'complete' && stream.runId ? (
                  <Link to={`/history/${stream.runId}#tab=longlist`}>
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">rank</span>
                    <span className="progress-row__status">
                      {progress.rank.rankedCount} ranked · top {progress.rank.topScore.toFixed(2)}
                    </span>
                  </Link>
                ) : (
                  <>
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">rank</span>
                    <span className="progress-row__status">
                      {progress.rank.rankedCount} ranked · top {progress.rank.topScore.toFixed(2)}
                    </span>
                  </>
                )}
              </li>
            )}
          </ul>
          </div></div>
          {stream.status === 'error' && stream.error && (
            <div className="error-banner">Search failed: {stream.error}</div>
          )}
        </section>
      )}

      {stream.status === 'complete' && stream.runId && (
        <section className="results">
          <h2 className="results__heading">
            Shortlist <span className="muted serif" style={{ fontStyle: 'italic' }}>({stream.shortlist.length})</span>
          </h2>
          {stream.shortlist.length === 0 && (
            <div className="muted">No matches met the threshold.</div>
          )}
          <div className="listing-list">
            {stream.shortlist.map(m => (
              <ListingCard key={m.id} match={m} runId={stream.runId!} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
