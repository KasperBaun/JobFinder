import type { RunSummary } from '../api/types'
import { formatRelative, formatAbsolute } from '../utils/time'

interface Props {
  run: RunSummary
}

export function RunSummaryCard({ run }: Props) {
  const ok = run.providers.filter(p => p.status === 'ok').length
  const failed = run.providers.filter(p => p.status === 'failed').length

  return (
    <div className="run-stat-bar">
      <div className="run-stat-bar__lead">
        <h2 className="run-stat-bar__id">Search</h2>
        <time
          className="run-stat-bar__time"
          title={formatAbsolute(run.startedAt)}
          dateTime={run.startedAt}
        >
          {formatRelative(run.startedAt)}
        </time>
      </div>

      <dl className="run-stat-bar__metrics">
        <div className={`stat${failed > 0 ? ' stat--bad' : ''}`}>
          <dt>Sources</dt>
          <dd>
            <span className="stat__num">{ok}</span> ok
            {failed > 0 && <> · <span className="stat__num">{failed}</span> failed</>}
          </dd>
        </div>
        <div className="stat">
          <dt>Jobs found</dt>
          <dd><span className="stat__num">{run.fetchedCount}</span></dd>
        </div>
        <div className="stat">
          <dt>Unique jobs</dt>
          <dd><span className="stat__num">{run.dedupedCount}</span></dd>
        </div>
        <div className="stat">
          <dt>Top jobs</dt>
          <dd><span className="stat__num">{run.shortlistCount}</span></dd>
        </div>
        <div className="stat">
          <dt>Best rating</dt>
          <dd><span className="stat__num">{run.topScore.toFixed(2)}</span></dd>
        </div>
        <div className="stat">
          <dt>Good matches</dt>
          <dd><span className="stat__num">{run.goodMarks}</span> / {run.shortlistCount}</dd>
        </div>
      </dl>
    </div>
  )
}
