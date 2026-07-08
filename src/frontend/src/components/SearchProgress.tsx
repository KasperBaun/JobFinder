import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getLlmStatus } from '../api/client'
import { isTerminalState } from '../api/types'
import type { JobSearch } from '../api/types'
import { PHASE_LABEL, STATE_LABEL } from '../utils/searchLabels'
import { buildRows, countSources, reached } from '../utils/progress'
import { PipelineSteps } from './PipelineSteps'
import { ProgressSummary } from './ProgressSummary'
import { SourceGrid } from './SourceGrid'
import { ActivityLog } from './ActivityLog'

type Props = {
  job: JobSearch
  providerNames: string[]
  succeeded: boolean
  stepsOpen: boolean
  onToggleSteps: () => void
}

// Orchestrates the live search-progress card: a numbered pipeline stepper + a compact summary
// (always visible), then the dense source grid and the collapsed activity log (behind the steps
// toggle). Owns the row/count derivation so the children stay presentational.
export function SearchProgress({ job, providerNames, succeeded, stepsOpen, onToggleSteps }: Props) {
  const llmQuery = useQuery({ queryKey: ['llm-status'], queryFn: getLlmStatus, refetchOnWindowFocus: false })
  const aiEnabled = !!llmQuery.data?.enabled && !!llmQuery.data?.modelPresent

  const rows = useMemo(() => buildRows(job, providerNames), [job, providerNames])
  const counts = useMemo(() => countSources(rows), [rows])

  const active = !isTerminalState(job.state)
  const statusBadge = isTerminalState(job.state) ? STATE_LABEL[job.state] : null
  const phaseLabel = statusBadge ?? PHASE_LABEL[job.phase]
  const showDedupe = job.dedupedCount > 0 || reached(job.phase, 'deduping')
  const showRank = job.rankedCount > 0 || reached(job.phase, 'ranking')

  return (
    <section className="progress-panel">
      <div className="progress-panel__head">
        <h2 className="progress-panel__heading">
          {phaseLabel}
          {job.attempt > 1 && !statusBadge && <span className="muted"> · attempt {job.attempt}</span>}
        </h2>
        <button type="button" className="link-button" onClick={onToggleSteps}>
          {stepsOpen ? 'hide steps ▴' : 'show steps ▾'}
        </button>
      </div>

      <PipelineSteps
        phase={job.phase}
        state={job.state}
        aiEnabled={aiEnabled}
        timeline={job.timeline}
        startedAt={job.startedAt}
        finishedAt={job.finishedAt}
        currentAttemptStartedAt={job.currentAttemptStartedAt}
      />

      <ProgressSummary
        job={job}
        counts={counts}
        active={active}
        showDedupe={showDedupe}
        showRank={showRank}
      />

      {stepsOpen && (
        <>
          <SourceGrid rows={rows} runId={job.id} linkable={succeeded} />
          <ActivityLog events={job.timeline} />
        </>
      )}

      {job.state === 'failed' && job.error && (
        <div className="error-banner">Search failed: {job.error}</div>
      )}
    </section>
  )
}
