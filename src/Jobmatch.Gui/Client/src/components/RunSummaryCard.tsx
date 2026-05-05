import type { RunSummary } from '../api/types'
import { formatRelative, formatAbsolute } from '../utils/time'

interface Props {
  run: RunSummary
}

export function RunSummaryCard({ run }: Props) {
  const ok = run.providers.filter(p => p.status === 'ok').length
  const failed = run.providers.filter(p => p.status === 'failed').length
  return (
    <div className="run-summary-card">
      <div className="run-summary-card__header">
        <h2 className="run-summary-card__title">Run {run.runId}</h2>
        <time
          className="run-summary-card__time"
          title={formatAbsolute(run.startedAt)}
          dateTime={run.startedAt}
        >
          {formatRelative(run.startedAt)}
        </time>
      </div>
      <dl className="definition-list">
        <dt>Providers</dt>
        <dd>{ok} ok / {failed} failed</dd>
        <dt>Fetched</dt>
        <dd>{run.fetchedCount}</dd>
        <dt>After dedupe</dt>
        <dd>{run.dedupedCount}</dd>
        <dt>Ranked</dt>
        <dd>{run.rankedCount}</dd>
        <dt>Shortlist</dt>
        <dd>{run.shortlistCount}</dd>
        <dt>Top score</dt>
        <dd>{run.topScore.toFixed(2)}</dd>
        <dt>Good marks</dt>
        <dd>{run.goodMarks} / {run.shortlistCount}</dd>
      </dl>
    </div>
  )
}
