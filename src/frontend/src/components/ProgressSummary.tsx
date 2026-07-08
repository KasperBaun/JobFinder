import type { JobSearch } from '../api/types'
import type { SourceCounts } from '../utils/progress'
import { useElapsed } from '../hooks/useElapsed'

type Props = {
  job: JobSearch
  counts: SourceCounts
  active: boolean
  showDedupe: boolean
  showRank: boolean
}

// Compact always-visible summary under the pipeline stepper: a sources progress bar, a live elapsed
// timer, and a one-line aggregate — all from fields already on the snapshot. Replaces the old
// page-long per-source list as the at-a-glance view.
export function ProgressSummary({ job, counts, active, showDedupe, showRank }: Props) {
  // Time from the current attempt's start so a run resumed after a host restart reflects the active run,
  // not the (possibly hours-long) gap the process was down (matches the stepper). Legacy runs without the
  // anchor fall back to startedAt.
  const elapsed = useElapsed(job.currentAttemptStartedAt ?? job.startedAt, job.finishedAt, active)
  const pct = counts.total > 0 ? Math.round((counts.done / counts.total) * 100) : 0

  return (
    <div className="progress-hero">
      <div className="progress-hero__bar-row">
        <div className="progress-bar progress-bar--fluid">
          <div className="progress-bar__fill" style={{ width: `${pct}%` }} />
        </div>
        <span className="progress-hero__frac tabular mono">{counts.done}/{counts.total}</span>
        <span className="progress-hero__timer tabular mono">{elapsed}</span>
      </div>
      <div className="progress-hero__aggregate tabular">
        {job.fetchedCount.toLocaleString()} jobs found
        {active && counts.running > 0 && (
          <> · <span className="progress-hero__running">{counts.running} running</span></>
        )}
        {counts.failed > 0 && (
          <> · <span className="progress-hero__failed">{counts.failed} failed</span></>
        )}
        {showDedupe && <> · {job.dedupedCount.toLocaleString()} unique</>}
        {showRank && <> · top {job.topScore.toFixed(2)}</>}
      </div>
    </div>
  )
}
