import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getApplications } from '../api/client'
import { StatusSelect } from '../components/StatusSelect'
import { formatAbsolute, formatRelative } from '../utils/time'
import type { ApplicationEntry } from '../api/types'

// Cross-run tracker: every listing that carries an application status, with the
// newest run's status when the same job was statused in several runs (R-097).
export function ApplicationsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['applications'],
    queryFn: getApplications,
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">05 / applications</div>
        <h1 className="page__heading">Your <em>applications</em></h1>
        <p className="page__lede">
          Every job you've tracked, across all searches. Interviews and offers teach the AI what a strong fit looks like.
        </p>
      </header>

      {isLoading && <div className="muted">Loading applications…</div>}
      {error && <div className="error-text">Failed to load applications.</div>}

      {data && data.applications.length === 0 && (
        <div className="hint-card">
          Nothing tracked yet. Set a status — like <em>Applied</em> — on any job in a{' '}
          <Link to="/history">past search</Link> to start tracking it here.
        </div>
      )}

      {data && data.applications.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Company</th>
                <th>Source</th>
                <th>Rating</th>
                <th>Status</th>
                <th>Your rating</th>
                <th>From search</th>
              </tr>
            </thead>
            <tbody>
              {data.applications.map(a => <ApplicationRow key={a.listingId} entry={a} />)}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function ApplicationRow({ entry }: { entry: ApplicationEntry }) {
  return (
    <tr>
      <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
      <td>{entry.company ?? <span className="muted">—</span>}</td>
      <td><span className="badge badge--muted">{entry.portalDisplayName ?? entry.portal}</span></td>
      <td className="tabular mono">{entry.score.toFixed(2)}</td>
      <td>
        <StatusSelect runId={entry.runId} listingId={entry.listingId} current={entry.status} compact />
      </td>
      <td>
        {entry.mark
          ? (
            <span
              className={`badge ${entry.mark === 'good' ? 'badge--score' : 'badge--muted'}`}
              title={entry.reason ? `“${entry.reason}”` : undefined}
            >
              {entry.mark === 'good' ? 'Good match' : 'Not a match'}
            </span>
          )
          : <span className="muted">—</span>}
      </td>
      <td title={formatAbsolute(entry.runStartedAt)}>
        <Link to={`/history/${entry.runId}`}>{formatRelative(entry.runStartedAt)}</Link>
      </td>
    </tr>
  )
}
