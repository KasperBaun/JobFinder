import type { JobSearchPhase, JobSearchState } from '../api/types'

export const PHASE_LABEL: Record<JobSearchPhase, string> = {
  pending: 'Queued',
  fetching: 'Fetching listings',
  deduping: 'Removing duplicates',
  ranking: 'Rating jobs',
  llmJudging: 'AI review',
  writing: 'Finishing up',
  done: 'Done',
}

export const STATE_LABEL: Record<JobSearchState, string> = {
  queued: 'Queued',
  running: 'Running',
  succeeded: 'Complete',
  failed: 'Failed',
  cancelled: 'Cancelled',
  interrupted: 'Interrupted',
}
