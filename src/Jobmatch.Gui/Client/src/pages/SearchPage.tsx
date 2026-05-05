import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getProviders } from '../api/client'
import { useSearchStream } from '../hooks/useSearchStream'
import { ListingCard } from '../components/ListingCard'
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
  const stream = useSearchStream()
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
        <h1 className="page__heading">Run a search</h1>
        <p className="page__lede">Fetch the latest listings from your enabled providers.</p>
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
        <div className="card advanced-panel">
          <div className="form-row">
            <label htmlFor="topN">Top N</label>
            <input
              id="topN"
              type="number"
              placeholder="from ranking.yml"
              value={topN}
              onChange={e => setTopN(e.target.value)}
              min={1}
            />
          </div>
          <div className="form-row">
            <label htmlFor="minScore">Min score</label>
            <input
              id="minScore"
              type="number"
              placeholder="from ranking.yml"
              value={minScore}
              onChange={e => setMinScore(e.target.value)}
              min={0}
              max={1}
              step={0.01}
            />
          </div>
          <div className="form-row form-row--column">
            <label>Providers</label>
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
      )}

      {stream.status === 'idle' && (
        <div className="hint-card">
          No search yet. Click "Run a search" to fetch the latest listings.
        </div>
      )}

      {stream.status !== 'idle' && (
        <section className="progress-panel">
          <h2 className="progress-panel__heading">Progress</h2>
          <ul className="progress-list">
            {progress.rows.map(row => (
              <li key={row.name} className={`progress-row progress-row--${row.status}`}>
                <span className="progress-row__name">{row.name}</span>
                <span className="progress-row__status">
                  {row.status === 'pending' && 'pending'}
                  {row.status === 'running' && 'running…'}
                  {row.status === 'ok' && `ok · ${row.fetchedCount ?? 0} fetched`}
                  {row.status === 'failed' && `failed: ${row.error ?? 'unknown error'}`}
                </span>
              </li>
            ))}
            {progress.dedupe !== undefined && (
              <li className="progress-row progress-row--info">
                <span className="progress-row__name">dedupe</span>
                <span className="progress-row__status">{progress.dedupe} after merge</span>
              </li>
            )}
            {progress.rank !== undefined && (
              <li className="progress-row progress-row--info">
                <span className="progress-row__name">rank</span>
                <span className="progress-row__status">
                  {progress.rank.rankedCount} ranked · top {progress.rank.topScore.toFixed(2)}
                </span>
              </li>
            )}
          </ul>
          {stream.status === 'error' && stream.error && (
            <div className="error-banner">Search failed: {stream.error}</div>
          )}
        </section>
      )}

      {stream.status === 'complete' && stream.runId && (
        <section className="results">
          <h2 className="results__heading">
            Shortlist ({stream.shortlist.length})
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
