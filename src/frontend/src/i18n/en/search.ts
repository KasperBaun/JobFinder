import type { JobSearchPhase, JobSearchState } from '../../api/types'

export const search = {
  // `satisfies` keeps the exhaustiveness check against the domain enums while still widening the
  // values to string, so the Danish catalog is not forced to repeat the English literals.
  phase: {
    pending: 'Queued',
    fetching: 'Fetching listings',
    deduping: 'Removing duplicates',
    ranking: 'Rating jobs',
    llmJudging: 'AI review',
    writing: 'Finishing up',
    done: 'Done',
  } satisfies Record<JobSearchPhase, string>,

  state: {
    queued: 'Queued',
    running: 'Running',
    succeeded: 'Complete',
    failed: 'Failed',
    cancelled: 'Cancelled',
    interrupted: 'Interrupted',
  } satisfies Record<JobSearchState, string>,
}
