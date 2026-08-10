import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getHistory, getProviders, getSetupStatus } from '../api/client'
import { useSearchRun } from '../context/SearchRunContext'
import { LlmModelBanner } from '../components/LlmModelBanner'
import { SearchProgress } from '../components/SearchProgress'
import { formatRelative } from '../utils/time'
import { dec, useT } from '../i18n'
import { lastCompletedRun } from '../utils/runs'
import type { ProviderSummary, SearchRequest } from '../api/types'

// A source is actually fetched only if the user has it on AND any required secret is set — this
// mirrors the backend's Prepare() filter (ProviderStateMerger.Merge). A secret-less source stays
// "enabled" in the list but never runs, so it must not seed the progress grid or the source total.
const isRunnableSource = (p: ProviderSummary) => p.enabled && (!p.requiresSecret || p.hasSecret)

export function SearchPage() {
  const t = useT('search')
  const common = useT('common')
  const providersQuery = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const historyQuery = useQuery({ queryKey: ['history'], queryFn: getHistory })
  const setupQuery = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })
  const { job, isActive, start, cancel, reset } = useSearchRun()

  const lastRun = lastCompletedRun(historyQuery.data?.runs)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [topN, setTopN] = useState<string>('')
  const [minScore, setMinScore] = useState<string>('')
  const [selectedProviders, setSelectedProviders] = useState<string[] | null>(null)
  const [stepsOpen, setStepsOpen] = useState(true)

  const enabledProviderNames = useMemo(
    () => providersQuery.data?.providers.filter(isRunnableSource).map(p => p.name) ?? [],
    [providersQuery.data],
  )
  const effectiveProviders = selectedProviders ?? enabledProviderNames

  const succeeded = job?.state === 'succeeded'

  // The run's history page is the one results surface, so a finishing run hands over to it.
  // Seeded with the mount-time id: a run that finished while this page was closed must not
  // bounce the user off the search page when they come back to start a new one.
  const navigate = useNavigate()
  const jobId = job?.id
  const redirectedFor = useRef<string | null>(succeeded ? jobId ?? null : null)
  useEffect(() => {
    if (succeeded && jobId && redirectedFor.current !== jobId) {
      redirectedFor.current = jobId
      navigate(`/history/${jobId}`)
    }
  }, [succeeded, jobId, navigate])

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
          <div className="page__eyebrow">{t.eyebrow}</div>
          <h1 className="page__heading">{t.heading()}</h1>
        </header>
        <div className="hint-card">
          <p>{t.profileFirst}</p>
          <Link to="/skillset" className="btn btn--primary">{t.setUpProfile}</Link>
        </div>
      </div>
    )
  }

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{t.heading()}</h1>
        <p className="page__lede">{t.lede}</p>
      </header>

      <LlmModelBanner />

      <div className="search-controls">
        <button
          type="button"
          className="btn btn--primary btn--lg"
          onClick={handleRun}
          disabled={isActive}
        >
          {isActive ? t.running : t.runSearch}
        </button>
        {isActive && (
          <button type="button" className="btn btn--secondary" onClick={() => void cancel()}>
            {common.cancel}
          </button>
        )}
        {job != null && !isActive && (
          <button type="button" className="btn btn--secondary" onClick={reset}>
            {t.reset}
          </button>
        )}
        <button type="button" className="link-button" onClick={() => setAdvancedOpen(o => !o)}>
          {advancedOpen ? t.hideOptions : t.moreOptions}
        </button>
      </div>

      {advancedOpen && (
        <section className="card advanced-panel">
          <div className="field-grid">
            <div className="field">
              <label className="field__label" htmlFor="topN">{t.topNLabel}</label>
              <input
                id="topN"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder={t.defaultPlaceholder}
                value={topN}
                onChange={e => setTopN(e.target.value)}
                min={1}
              />
            </div>
            <div className="field">
              <label className="field__label" htmlFor="minScore">{t.minScoreLabel}</label>
              <input
                id="minScore"
                type="number"
                className="input input--narrow input--mono tabular"
                placeholder={t.defaultPlaceholder}
                value={minScore}
                onChange={e => setMinScore(e.target.value)}
                min={0}
                max={1}
                step={0.01}
              />
            </div>
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label">{t.sourcesLabel}</label>
              <div className="chip-group">
                {providersQuery.data?.providers.map(p => {
                  const active = effectiveProviders.includes(p.name)
                  const needsSecret = !!p.requiresSecret && !p.hasSecret
                  return (
                    <button
                      key={p.name}
                      type="button"
                      className={active ? 'chip chip--active' : 'chip'}
                      onClick={() => toggleProvider(p.name)}
                      disabled={(!p.enabled || needsSecret) && !active}
                      title={
                        !p.enabled
                          ? t.sourceTurnedOff
                          : needsSecret
                            ? t.sourceNeedsKey
                            : ''
                      }
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
              {t.lastSearchSummary(
                formatRelative(lastRun.startedAt),
                lastRun.shortlistCount,
                dec(lastRun.topScore, 2),
              )}
              <br />
              {t.runToRefresh(
                <Link to={`/history/${lastRun.runId}`}>{t.viewThatSearch}</Link>,
              )}
            </>
          ) : (
            t.readyWhenYouAre()
          )}
        </div>
      )}

      {job != null && (
        <SearchProgress
          job={job}
          providerNames={effectiveProviders}
          succeeded={succeeded}
          stepsOpen={stepsOpen}
          onToggleSteps={() => setStepsOpen(o => !o)}
        />
      )}
    </div>
  )
}
