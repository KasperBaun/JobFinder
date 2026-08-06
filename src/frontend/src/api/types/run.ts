import type {
  DedupeGroup,
  DroppedEntry,
  ListingMatch,
  ProviderRaw,
  ScoredEntry,
} from './listing'
import type { ApplicationStatus } from './marks'
import type { JobSearchEvent, JobSearchPhase, JobSearchState, ProviderRunStatus } from './search'

export type RunSummary = {
  runId: string
  startedAt: string
  providers: ProviderRunStatus[]
  fetchedCount: number
  dedupedCount: number
  rankedCount: number
  shortlistCount: number
  topScore: number
  goodMarks: number
  state?: JobSearchState
  phase?: JobSearchPhase
}

export type HistoryResponse = { runs: RunSummary[] }

export type RunDetail = RunSummary & {
  shortlist: ListingMatch[]
  marks: Record<string, 'good' | 'bad'>
  markReasons?: Record<string, string>
  markStatuses?: Record<string, ApplicationStatus>
  /** ISO timestamp of the last status change per listing; absent for statuses set before R-107. */
  markStatusAt?: Record<string, string>
  raw?: ProviderRaw[]
  dedupeMerges?: DedupeGroup[]
  scored?: ScoredEntry[]
  dropped?: DroppedEntry[]
  timeline?: JobSearchEvent[]
}

export type DeleteHistoryRequest = { runIds: string[] }
export type DeleteHistoryResponse = { deleted: number; missing: string[]; error?: string }
