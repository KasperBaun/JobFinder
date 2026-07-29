import type { ReactNode } from 'react'
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

  steps: {
    fetching: 'Retrieve jobs',
    deduping: 'Deduplicate',
    ranking: 'Rate matches',
    llmJudging: 'AI review',
    done: 'Done',
  },
  stepsAria: 'Search steps',

  eyebrow: '03 / search',
  heading: () => <>Run a <em>search</em></>,
  lede:
    'Pulls the latest listings from your active sources, removes duplicates, rates them against '
    + 'your profile, and shows you the top picks. The search keeps running even if you move to '
    + 'another page or reload.',

  profileFirst: 'Set up your profile first — jobfinder rates every listing against it.',
  setUpProfile: 'Set up your profile',

  running: 'Running…',
  searchRunning: 'Search running',
  runSearch: 'Run a search',
  reset: 'Reset',
  hideOptions: 'Hide options',
  moreOptions: 'More options…',

  topNLabel: 'Number of top jobs',
  minScoreLabel: 'Minimum rating',
  defaultPlaceholder: 'default',
  sourcesLabel: 'Sources',
  sourceTurnedOff: 'Turned off on the Sources page',
  sourceNeedsKey: 'Needs an API key — set it on the Sources page',

  lastSearchSummary: (when: ReactNode, count: ReactNode, score: ReactNode) => (
    <>
      Last search was <strong>{when}</strong> — <span className="tabular">{count}</span> top jobs,
      best rating <span className="tabular mono">{score}</span>.
    </>
  ),
  runToRefresh: (viewLink: ReactNode) => (
    <>Click <strong>Run a search</strong> to refresh, or {viewLink}.</>
  ),
  viewThatSearch: 'view that search',
  readyWhenYouAre: () => (
    <>Ready when you are. Click <strong>Run a search</strong> to pull the latest listings.</>
  ),

  attempt: (n: number) => ` · attempt ${n}`,
  hideSteps: 'hide steps ▴',
  showSteps: 'show steps ▾',
  searchFailed: (error: string) => `Search failed: ${error}`,

  jobsFound: (count: string) => `${count} jobs found`,
  sourcesRunning: (count: number) => `${count} running`,
  sourcesFailed: (count: number) => `${count} failed`,
  unique: (count: string) => `${count} unique`,
  topScore: (score: string) => `top ${score}`,

  activityLog: (count: number) => `Activity log (${count} events)`,

  topJobs: 'Top jobs',
  loadingResults: 'Loading results…',
  noJobsMetMinimum: 'No jobs met the minimum rating.',
}
