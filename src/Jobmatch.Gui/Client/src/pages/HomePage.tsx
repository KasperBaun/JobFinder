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

  return (
    <div className="page page--wide">
      <header className="page__header">
        <h1 className="page__heading">Dashboard</h1>
        <p className="page__lede">Overview of your jobfinder workspace.</p>
      </header>

      <div className="stat-grid">
        <StatCard
          label="Signed in as"
          value={
            whoami.isLoading ? <span className="muted">Loading…</span> :
            whoami.error || !whoami.data ? <span className="error-text">Error</span> :
            whoami.data.email
          }
          subtitle={whoami.data && <span className="mono small">{whoami.data.dataDir}</span>}
        />
        <StatCard
          label="Providers"
          value={
            providers.isLoading ? <span className="muted">Loading…</span> :
            providers.error || !providers.data ? <span className="error-text">Error</span> :
            `${enabledCount} enabled`
          }
          subtitle={totalCount !== undefined ? `${totalCount} configured` : undefined}
          link="/providers"
        />
        <StatCard
          label="Last run"
          value={
            history.isLoading ? <span className="muted">Loading…</span> :
            history.error ? <span className="error-text">Error</span> :
            !lastRun ? <span className="muted">No runs yet</span> :
            formatRelative(lastRun.startedAt)
          }
          subtitle={
            lastRun && (
              <span>
                {lastRun.shortlistCount} shortlisted · top {lastRun.topScore.toFixed(2)}
              </span>
            )
          }
          link={lastRun ? `/history/${lastRun.runId}` : '/history'}
        />
        <StatCard
          label="Good marks"
          value={
            history.isLoading ? <span className="muted">Loading…</span> :
            history.error ? <span className="error-text">Error</span> :
            totalGoodMarks
          }
          subtitle="across all runs"
          link="/history"
        />
      </div>

      <div className="cta-row">
        <Link to="/search" className="btn btn--primary btn--lg">
          Run a new search
        </Link>
      </div>
    </div>
  )
}
