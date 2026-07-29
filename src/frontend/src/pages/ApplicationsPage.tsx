import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getApplications } from '../api/client'
import { StatusSelect } from '../components/StatusSelect'
import { formatAbsolute, formatRelative } from '../utils/time'
import type { ApplicationEntry } from '../api/types'
import { dec, useT } from '../i18n'

// Cross-run tracker: every listing that carries an application status, with the
// newest run's status when the same job was statused in several runs (R-097).
export function ApplicationsPage() {
  const t = useT('applications')
  const { data, isLoading, error } = useQuery({
    queryKey: ['applications'],
    queryFn: getApplications,
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{t.heading()}</h1>
        <p className="page__lede">{t.lede}</p>
      </header>

      {isLoading && <div className="muted">{t.loading}</div>}
      {error && <div className="error-text">{t.loadFailed}</div>}

      {data && data.applications.length === 0 && (
        <div className="hint-card">
          {t.emptyPrefix} <em>{t.emptyStatus}</em> {t.emptyMiddle}{' '}
          <Link to="/history">{t.emptyLink}</Link> {t.emptySuffix}
        </div>
      )}

      {data && data.applications.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t.colTitle}</th>
                <th>{t.colCompany}</th>
                <th>{t.colSource}</th>
                <th>{t.colRating}</th>
                <th>{t.colStatus}</th>
                <th>{t.colYourRating}</th>
                <th>{t.colFromSearch}</th>
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
  const t = useT('listing')
  return (
    <tr>
      <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
      <td>{entry.company ?? <span className="muted">—</span>}</td>
      <td><span className="badge badge--muted">{entry.portalDisplayName ?? entry.portal}</span></td>
      <td className="tabular mono">{dec(entry.score, 2)}</td>
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
              {entry.mark === 'good' ? t.markGood : t.markBad}
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
