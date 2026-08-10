export type SearchRequest = {
  providers?: string[]
  topN?: number
  minScore?: number
}

export type ProviderRunStatus = {
  name: string
  status: 'pending' | 'running' | 'ok' | 'failed'
  fetchedCount?: number
  error?: string
  durationMs?: number
  hitPageCap?: boolean
  possiblyCapped?: boolean
}

// Background search lifecycle — mirrors the backend JobSearch aggregate. Enum values are camelCase
// to match the API's JsonStringEnumConverter (e.g. JobSearchPhase.LlmJudging → "llmJudging").
export type JobSearchState =
  | 'queued'
  | 'running'
  | 'succeeded'
  | 'failed'
  | 'cancelled'
  | 'interrupted'

export type JobSearchPhase =
  | 'pending'
  | 'fetching'
  | 'deduping'
  | 'ranking'
  | 'llmJudging'
  | 'writing'
  | 'done'

export type JobSearchEvent = {
  timestamp: string
  level: 'info' | 'warn' | 'error'
  phase: JobSearchPhase
  /** English prose. Kept as the fallback for runs recorded before `messageKey` existed. */
  message: string
  messageKey?: string
  args?: Record<string, unknown>
  provider?: string
  count?: number
  durationMs?: number
}

export type JobSearch = {
  id: string
  state: JobSearchState
  phase: JobSearchPhase
  request: SearchRequest
  createdAt: string
  startedAt?: string
  finishedAt?: string
  // Start of the current run attempt (resets on resume); absent on legacy runs. See JobSearch.cs.
  currentAttemptStartedAt?: string
  providers: ProviderRunStatus[]
  fetchedCount: number
  dedupedCount: number
  rankedCount: number
  shortlistCount: number
  topScore: number
  error?: string
  hangfireJobId?: string
  attempt: number
  lastHeartbeat: string
  timeline: JobSearchEvent[]
}

export type StartSearchResponse = { id: string }

export const JOB_SEARCH_TERMINAL_STATES: JobSearchState[] = [
  'succeeded',
  'failed',
  'cancelled',
  'interrupted',
]

export function isTerminalState(state: JobSearchState): boolean {
  return JOB_SEARCH_TERMINAL_STATES.includes(state)
}
