import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getHistory, getProviders, getWhoami } from '../api/client'
import { StatCard } from '../components/StatCard'
import { formatRelative } from '../utils/time'

export function HomePage() {
  const whoami = useQuery({ queryKey: ['whoami'], queryFn: getWhoami })
  const providers = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const history = useQuery({ queryKey: ['history'], queryFn: getHistory })

  const enabledCount = providers.data?.providers.filter(p => p.enabled).length
  const totalCount = providers.data?.providers.length
  const lastRun = history.data?.runs[0]
  const totalGoodMarks = history.data?.runs.reduce((sum, r) => sum + r.goodMarks, 0) ?? 0
  const recent = history.data?.runs.slice(0, 4) ?? []

  return (
    <div className="page page--wide">
      <section className="hero">
        <div className="page__eyebrow">00 / dashboard</div>
        <h1 className="hero__headline">Find work that <em>fits.</em></h1>
        <p className="hero__lede">
          A local tool that pulls openings from your chosen portals, ranks them against your skillset,
          and gets out of your way. No telemetry. No cloud. Your data, your machine.
        </p>
        <div className="hero__meta">
          <span className="hero__meta-item">
            <span className="hero__meta-dot" />
            {whoami.data?.email ?? 'loading…'}
          </span>
          {whoami.data && <span className="muted">→ {whoami.data.dataDir}</span>}
        </div>
        <div className="cta-row">
          <Link to="/search" className="btn btn--primary btn--lg">Run a new search</Link>
          <Link to="/skillset" className="btn btn--secondary btn--lg">Edit skillset</Link>
        </div>
      </section>

      <div className="section-head">
        <h2 className="section-title">At a glance</h2>
      </div>

      <div className="stat-grid">
        <StatCard
          label="Providers"
          value={
            providers.isLoading ? <span className="muted">…</span> :
            providers.error || !providers.data ? <span className="error-text">err</span> :
            <span><span className="tabular">{enabledCount}</span> <span className="subtle small">enabled</span></span>
          }
          subtitle={totalCount !== undefined ? `${totalCount} configured` : undefined}
          link="/providers"
        />
        <StatCard
          label="Last run"
          value={
            history.isLoading ? <span className="muted">…</span> :
            history.error ? <span className="error-text">err</span> :
            !lastRun ? <span className="muted">No runs yet</span> :
            formatRelative(lastRun.startedAt)
          }
          subtitle={
            lastRun && (
              <span>
                <span className="tabular">{lastRun.shortlistCount}</span> shortlisted · top <span className="tabular mono">{lastRun.topScore.toFixed(2)}</span>
              </span>
            )
          }
          link={lastRun ? `/history/${lastRun.runId}` : '/history'}
        />
        <StatCard
          label="Good marks"
          value={
            history.isLoading ? <span className="muted">…</span> :
            history.error ? <span className="error-text">err</span> :
            <span className="tabular">{totalGoodMarks}</span>
          }
          subtitle="across all runs"
          link="/history"
        />
        <StatCard
          label="Skillset"
          value={<span className="serif" style={{ fontSize: '1.4rem' }}>configured</span>}
          subtitle="primary stack, domains, disqualifiers"
          link="/skillset"
        />
      </div>

      {recent.length > 0 && (
        <>
          <div className="section-head">
            <h2 className="section-title">Recent runs</h2>
            <Link to="/history" className="link-button">View all →</Link>
          </div>
          <div className="table-wrap">
            <table className="table table--clickable">
              <thead>
                <tr>
                  <th>Started</th>
                  <th>Shortlist</th>
                  <th>Top score</th>
                  <th>Good marks</th>
                </tr>
              </thead>
              <tbody>
                {recent.map(r => (
                  <tr key={r.runId} onClick={() => window.location.assign(`/history/${r.runId}`)}>
                    <td>
                      <Link to={`/history/${r.runId}`} onClick={(e) => e.stopPropagation()}>
                        {formatRelative(r.startedAt)}
                      </Link>
                    </td>
                    <td className="tabular">{r.shortlistCount}</td>
                    <td className="tabular mono">{r.topScore.toFixed(2)}</td>
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
