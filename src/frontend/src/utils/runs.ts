import type { RunSummary } from '../api/types'

// The most recent run that actually produced results. Both the Overview and the Search page
// summarise "your last search" from this, so they can't drift apart and neither surfaces a
// cancelled/interrupted run's empty 0-jobs / 0.00 stats as if it were the real outcome.
// (state === undefined keeps legacy runs recorded before the state field was added.)
export function lastCompletedRun(runs: RunSummary[] | undefined): RunSummary | undefined {
  return runs?.find(r => r.state === 'succeeded' || r.state === undefined)
}
