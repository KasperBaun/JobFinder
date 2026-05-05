import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getHistory, getRun } from '../api/client'
import { ListingCard } from '../components/ListingCard'
import { RunSummaryCard } from '../components/RunSummaryCard'
import { formatAbsolute, formatRelative } from '../utils/time'

function HistoryListView() {
  const navigate = useNavigate()
  const { data, isLoading, error } = useQuery({
    queryKey: ['history'],
    queryFn: getHistory,
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <h1 className="page__heading">History</h1>
        <p className="page__lede">Past searches and how they ranked.</p>
      </header>

      {isLoading && <div className="muted">Loading history…</div>}
      {error && <div className="error-text">Failed to load history.</div>}

      {data && data.runs.length === 0 && (
        <div className="hint-card">No runs yet. Start one from the Search page.</div>
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
                  <tr
                    key={run.runId}
                    onClick={() => navigate(`/history/${run.runId}`)}
                    style={{ cursor: 'pointer' }}
                  >
                    <td title={formatAbsolute(run.startedAt)}>
                      <Link to={`/history/${run.runId}`} onClick={e => e.stopPropagation()}>
                        {formatRelative(run.startedAt)}
                      </Link>
                    </td>
                    <td>{ok} ok / {failed} failed</td>
                    <td>{run.shortlistCount}</td>
                    <td>{run.topScore.toFixed(2)}</td>
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

function RunDetailView({ runId }: { runId: string }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['run', runId],
    queryFn: () => getRun(runId),
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <Link to="/history" className="back-link">← Back to history</Link>
        <h1 className="page__heading">Run detail</h1>
      </header>

      {isLoading && <div className="muted">Loading run…</div>}
      {error && <div className="error-text">Failed to load run.</div>}

      {data && (
        <>
          <RunSummaryCard run={data} />
          <section className="results">
            <h2 className="results__heading">Shortlist ({data.shortlist.length})</h2>
            {data.shortlist.length === 0 && (
              <div className="muted">No listings on this run's shortlist.</div>
            )}
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
