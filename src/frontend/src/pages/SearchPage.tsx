import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getHistory, getProviders, getRun, getSetupStatus } from '../api/client'
import { useSearchRun } from '../context/SearchRunContext'
import { ListingCard } from '../components/ListingCard'
import { LlmModelBanner } from '../components/LlmModelBanner'
import { formatRelative } from '../utils/time'
import { PHASE_LABEL, STATE_LABEL } from '../utils/searchLabels'
import { isTerminalState } from '../api/types'
import type { JobSearch, SearchRequest } from '../api/types'

type ProviderRowState = {
  name: string
  status: 'pending' | 'running' | 'ok' | 'failed'
  fetchedCount?: number
  error?: string
}

function buildRows(job: JobSearch | null, initialNames: string[]): ProviderRowState[] {
  const rows = new Map<string, ProviderRowState>()
  for (const name of initialNames) rows.set(name, { name, status: 'pending' })
  for (const p of job?.providers ?? []) {
    rows.set(p.name, { name: p.name, status: p.status, fetchedCount: p.fetchedCount, error: p.error })
  }
  return Array.from(rows.values())
}

function reached(phase: JobSearch['phase'], target: JobSearch['phase']): boolean {
  const order: JobSearch['phase'][] = ['pending', 'fetching', 'deduping', 'ranking', 'llmJudging', 'writing', 'done']
  return order.indexOf(phase) >= order.indexOf(target)
}

export function SearchPage() {
  const providersQuery = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const historyQuery = useQuery({ queryKey: ['history'], queryFn: getHistory })
  const setupQuery = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })
  const { job, isActive, start, cancel, reset } = useSearchRun()

  const lastRun = historyQuery.data?.runs.find(r => r.state === 'succeeded' || r.state === undefined)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [topN, setTopN] = useState<string>('')
  const [minScore, setMinScore] = useState<string>('')
  const [selectedProviders, setSelectedProviders] = useState<string[] | null>(null)
  const [stepsOpen, setStepsOpen] = useState(true)

  const enabledProviderNames = useMemo(
    () => providersQuery.data?.providers.filter(p => p.enabled).map(p => p.name) ?? [],
    [providersQuery.data],
  )
  const effectiveProviders = selectedProviders ?? enabledProviderNames

  const rows = useMemo(() => buildRows(job, effectiveProviders), [job, effectiveProviders])

  const succeeded = job?.state === 'succeeded'
  const runDetailQuery = useQuery({
    queryKey: ['run', job?.id],
    queryFn: () => getRun(job!.id),
    enabled: succeeded && !!job?.id,
  })

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
    setStepsOpen(true)
    void start(req)
  }

  if (setupQuery.data && !setupQuery.data.profileExists) {
    return (
      <div className="page page--wide">
        <header className="page__header">
          <div className="page__eyebrow">03 / search</div>
          <h1 className="page__heading">Run a <em>search</em></h1>
        </header>
        <div className="hint-card">
          <p>Set up your profile first — jobfinder rates every listing against it.</p>
          <Link to="/skillset" className="btn btn--primary">Set up your profile</Link>
        </div>
      </div>
    )
  }

  const showDedupe = job != null && (job.dedupedCount > 0 || reached(job.phase, 'deduping'))
  const showRank = job != null && (job.rankedCount > 0 || reached(job.phase, 'ranking'))
  const statusBadge = job && isTerminalState(job.state) ? STATE_LABEL[job.state] : null

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">03 / search</div>
        <h1 className="page__heading">Run a <em>search</em></h1>
        <p className="page__lede">
          Pulls the latest listings from your active sources, removes duplicates, rates them against your profile,
          and shows you the top picks. The search keeps running even if you move to another page or reload.
        </p>
      </header>

      <LlmModelBanner />

      <div className="search-controls">
        <button
          type="button"
          className="btn btn--primary btn--lg"
          onClick={handleRun}
          disabled={isActive}
        >
          {isActive ? 'Running…' : 'Run a search'}
        </button>
        {isActive && (
          <button type="button" className="btn btn--secondary" onClick={() => void cancel()}>
            Cancel
          </button>
        )}
        {job != null && !isActive && (
          <button type="button" className="btn btn--secondary" onClick={reset}>
            Reset
          </button>
        )}
        <button type="button" className="link-button" onClick={() => setAdvancedOpen(o => !o)}>
          {advancedOpen ? 'Hide options' : 'More options…'}
        </button>
      </div>

      {advancedOpen && (
        <section className="card advanced-panel">
          <div className="field-grid">
            <div className="field">
              <label className="field__label" htmlFor="topN">Number of top jobs</label>
              <input
                id="topN"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder="default"
                value={topN}
                onChange={e => setTopN(e.target.value)}
                min={1}
              />
            </div>
            <div className="field">
              <label className="field__label" htmlFor="minScore">Minimum rating</label>
              <input
                id="minScore"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder="default"
                value={minScore}
                onChange={e => setMinScore(e.target.value)}
                min={0}
                max={1}
                step={0.01}
              />
            </div>
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label">Sources</label>
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
                      title={p.enabled ? '' : 'Turned off on the Sources page'}
                    >
                      {p.displayName}
                    </button>
                  )
                })}
              </div>
            </div>
          </div>
        </section>
      )}

      {job == null && (
        <div className="hint-card">
          {lastRun ? (
            <>
              Last search was <strong>{formatRelative(lastRun.startedAt)}</strong> —{' '}
              <span className="tabular">{lastRun.shortlistCount}</span> top jobs, best rating{' '}
              <span className="tabular mono">{lastRun.topScore.toFixed(2)}</span>.
              <br />
              Click <strong>Run a search</strong> to refresh, or{' '}
              <Link to={`/history/${lastRun.runId}`}>view that search</Link>.
            </>
          ) : (
            <>Ready when you are. Click <strong>Run a search</strong> to pull the latest listings.</>
          )}
        </div>
      )}

      {job != null && (
        <section className="progress-panel">
          <div className="progress-panel__head">
            <h2 className="progress-panel__heading">
              {statusBadge ?? PHASE_LABEL[job.phase]}
              {job.attempt > 1 && !statusBadge && <span className="muted"> · attempt {job.attempt}</span>}
            </h2>
            <button type="button" className="link-button" onClick={() => setStepsOpen(o => !o)}>
              {stepsOpen ? 'hide steps ▴' : 'show steps ▾'}
            </button>
          </div>

          {stepsOpen && (
            <>
              <ul className="progress-list">
                {rows.map(row => {
                  const linkable = succeeded && row.status === 'ok'
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
                  return (
                    <li key={row.name} className={`progress-row progress-row--${row.status}${linkable ? ' progress-row--link' : ''}`}>
                      {linkable ? (
                        <Link to={`/history/${job.id}#tab=raw&provider=${encodeURIComponent(row.name)}`}>{body}</Link>
                      ) : body}
                    </li>
                  )
                })}
                {showDedupe && (
                  <li className="progress-row progress-row--info">
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">remove duplicates</span>
                    <span className="progress-row__status">{job.dedupedCount} unique jobs</span>
                  </li>
                )}
                {showRank && (
                  <li className="progress-row progress-row--info">
                    <span className="progress-row__icon">·</span>
                    <span className="progress-row__name">rate jobs</span>
                    <span className="progress-row__status">
                      {job.rankedCount} rated · best {job.topScore.toFixed(2)}
                    </span>
                  </li>
                )}
              </ul>

              {job.timeline.length > 0 && (
                <ol className="timeline">
                  {job.timeline.map((ev, i) => (
                    <li key={i} className={`timeline__item timeline__item--${ev.level}`}>
                      <span className="timeline__time tabular mono">{formatRelative(ev.timestamp)}</span>
                      <span className="timeline__msg">{ev.message}</span>
                    </li>
                  ))}
                </ol>
              )}
            </>
          )}

          {job.state === 'failed' && job.error && (
            <div className="error-banner">Search failed: {job.error}</div>
          )}
        </section>
      )}

      {succeeded && job && (
        <section className="results">
          <h2 className="results__heading">
            Top jobs{' '}
            <span className="muted serif" style={{ fontStyle: 'italic' }}>
              ({runDetailQuery.data?.shortlist.length ?? job.shortlistCount})
            </span>
          </h2>
          {runDetailQuery.isLoading && <div className="muted">Loading results…</div>}
          {runDetailQuery.data?.shortlist.length === 0 && (
            <div className="muted">No jobs met the minimum rating.</div>
          )}
          <div className="listing-list">
            {runDetailQuery.data?.shortlist.map(m => (
              <ListingCard key={m.id} match={m} runId={job.id} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
