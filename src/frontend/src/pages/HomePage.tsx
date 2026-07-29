import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getHistory, getProviders, getSetupStatus, getWhoami } from '../api/client'
import { StatCard } from '../components/StatCard'
import { dec, useT } from '../i18n'
import { formatRelative } from '../utils/time'
import { lastCompletedRun } from '../utils/runs'

export function HomePage() {
  const t = useT('home')
  const states = useT('search').state
  const whoami = useQuery({ queryKey: ['whoami'], queryFn: getWhoami })
  const providers = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const history = useQuery({ queryKey: ['history'], queryFn: getHistory })
  const setup = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })

  const enabledCount = providers.data?.providers.filter(p => p.enabled).length
  const totalCount = providers.data?.providers.length
  const lastRun = lastCompletedRun(history.data?.runs)
  const totalGoodMarks = history.data?.runs.reduce((sum, r) => sum + r.goodMarks, 0) ?? 0
  const recent = history.data?.runs.slice(0, 4) ?? []

  return (
    <div className="page page--wide">
      <section className="hero">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="hero__headline">{t.headline()}</h1>
        <p className="hero__lede">{t.lede}</p>
        <div className="hero__meta">
          <span className="hero__meta-item">
            <span className="hero__meta-dot" />
            {whoami.data?.email ?? t.loading}
          </span>
          {whoami.data && <span className="muted">→ {whoami.data.dataDir}</span>}
        </div>
        <div className="cta-row">
          <Link to="/search" className="btn btn--primary btn--lg">{t.runSearch}</Link>
          <Link to="/skillset" className="btn btn--secondary btn--lg">{t.editProfile}</Link>
        </div>
      </section>

      <div className="section-head">
        <h2 className="section-title">{t.atAGlance}</h2>
      </div>

      <div className="stat-grid">
        <StatCard
          label={t.sources}
          value={
            providers.isLoading ? <span className="muted">…</span> :
            providers.error || !providers.data ? <span className="error-text">{t.errorShort}</span> :
            <span><span className="tabular">{enabledCount}</span> <span className="subtle small">{t.sourcesOn}</span></span>
          }
          subtitle={totalCount !== undefined ? t.sourcesSetUp(totalCount) : undefined}
          link="/providers"
        />
        <StatCard
          label={t.lastSearch}
          value={
            history.isLoading ? <span className="muted">…</span> :
            history.error ? <span className="error-text">{t.errorShort}</span> :
            !lastRun ? <span className="muted">{t.noSearchesYet}</span> :
            formatRelative(lastRun.startedAt)
          }
          subtitle={
            lastRun && (
              <span>
                <span className="tabular">{lastRun.shortlistCount}</span> {t.topJobs} · {t.best} <span className="tabular mono">{dec(lastRun.topScore, 2)}</span>
              </span>
            )
          }
          link={lastRun ? `/history/${lastRun.runId}` : '/history'}
        />
        <StatCard
          label={t.goodMatches}
          value={
            history.isLoading ? <span className="muted">…</span> :
            history.error ? <span className="error-text">{t.errorShort}</span> :
            <span className="tabular">{totalGoodMarks}</span>
          }
          subtitle={t.acrossAllSearches}
          link="/history"
        />
        <StatCard
          label={t.profile}
          value={
            setup.isLoading ? <span className="muted">…</span> :
            setup.data?.profileExists
              ? <span className="serif" style={{ fontSize: '1.4rem' }}>{t.profileReady}</span>
              : <span className="serif" style={{ fontSize: '1.4rem' }}>{t.profileNotSetUp}</span>
          }
          subtitle={setup.data?.profileExists ? t.profileReadyHint : t.profileFinishHint}
          link="/skillset"
        />
      </div>

      {recent.length > 0 && (
        <>
          <div className="section-head">
            <h2 className="section-title">{t.recentSearches}</h2>
            <Link to="/history" className="link-button">{t.viewAll}</Link>
          </div>
          <div className="table-wrap">
            <table className="table table--clickable">
              <thead>
                <tr>
                  <th>{t.colWhen}</th>
                  <th>{t.colTopJobs}</th>
                  <th>{t.colBestRating}</th>
                  <th>{t.colGoodMatches}</th>
                </tr>
              </thead>
              <tbody>
                {recent.map(r => (
                  <tr key={r.runId} onClick={() => window.location.assign(`/history/${r.runId}`)}>
                    <td>
                      <Link to={`/history/${r.runId}`} onClick={(e) => e.stopPropagation()}>
                        {formatRelative(r.startedAt)}
                      </Link>
                      {r.state && r.state !== 'succeeded' && (
                        <span className="muted small"> · {states[r.state]}</span>
                      )}
                    </td>
                    <td className="tabular">{r.shortlistCount}</td>
                    <td className="tabular mono">{dec(r.topScore, 2)}</td>
                    <td className="tabular">{r.goodMarks} / {r.shortlistCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}
