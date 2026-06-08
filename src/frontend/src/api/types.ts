export type WhoamiResponse = {
  email: string
  dataDir: string
  toolVersion: string
}

export type ProviderType = 'api' | 'rss' | 'html' | 'manual'

export type ProviderSummary = {
  id: number
  name: string
  displayName: string
  type: ProviderType
  enabled: boolean
  endpoint?: string
  rateLimitRps: number
  notes?: string
  lastFetchedAt?: string
  lastFetchCount?: number
  requiresSecret?: string
  hasSecret: boolean
}

export type ProvidersResponse = { providers: ProviderSummary[] }

export type ProviderRecentRun = {
  runId: string
  startedAt: string
  status: string
  fetchedCount?: number
  error?: string
}

export type ProviderDetail = ProviderSummary & {
  recentRuns: ProviderRecentRun[]
}

export type ProviderEnabledUpdate = { enabled: boolean }

export type SetSecretsRequest = { values: Record<string, string> }

export type ProviderTestResult = {
  ok: boolean
  fetchedCount: number
  durationMs: number
  sampleTitle?: string
  error?: string
  testedAt: string
}

export type CreateResponse = { success: boolean; id: number; error?: string }

export type SkillsetResponse = {
  name: string
  location: string
  experienceYears: number
  targetRoles: string[]
  remotePreference: string
  seniority: string
  primaryStack: string[]
  secondaryStack: string[]
  domains: string[]
  disqualifiers: string[]
  languages: string[]
  employmentTypes: string[]
  country?: string | null
  region?: string | null
  metro: string[]
}

export type SkillsetUpdateRequest = {
  name: string
  location: string
  experienceYears: number
  targetRoles: string[]
  remotePreference: string
  seniority: string
  primaryStack: string[]
  secondaryStack: string[]
  domains: string[]
  disqualifiers: string[]
  languages: string[]
  employmentTypes: string[]
  country?: string | null
  region?: string | null
  metro: string[]
}

export type SearchRequest = {
  providers?: string[]
  topN?: number
  minScore?: number
}

export type ListingMatch = {
  id: string
  portal: string
  portalDisplayName?: string
  title: string
  company?: string
  location?: string
  remoteMode: string
  url: string
  postedAt?: string
  score: number
  reasoning: string
  primaryStackHits: string[]
  secondaryStackHits: string[]
}

export type ProviderRunStatus = {
  name: string
  status: 'pending' | 'running' | 'ok' | 'failed'
  fetchedCount?: number
  error?: string
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
  message: string
  provider?: string
  count?: number
}

export type JobSearch = {
  id: string
  state: JobSearchState
  phase: JobSearchPhase
  request: SearchRequest
  createdAt: string
  startedAt?: string
  finishedAt?: string
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

export type ScoreBreakdown = {
  primaryStack: number
  secondaryStack: number
  seniority: number
  locationRemote: number
  domain: number
  freshness: number
  disqualifierPenalty: number
}

export type RawListing = {
  id: string
  title: string
  company?: string
  location?: string
  url: string
  postedAt?: string
}

export type ProviderRaw = {
  provider: string
  listings: RawListing[]
}

export type DedupeGroup = {
  canonicalId: string
  mergedFromIds: string[]
}

export type ScoredEntry = {
  id: string
  title: string
  company?: string
  location?: string
  url: string
  postedAt?: string
  portal: string
  portalDisplayName?: string
  score: number
  breakdown: ScoreBreakdown
  primaryStackHits: string[]
  secondaryStackHits: string[]
}

export type DropReason =
  | 'disqualifier'
  | 'below_min_score'
  | 'beyond_top_n'
  | 'above_max_age'
  | 'missing_required_primary'

export type DroppedEntry = {
  id: string
  title: string
  company?: string
  score: number
  reason: DropReason
  context?: string
}

export type RunDetail = RunSummary & {
  shortlist: ListingMatch[]
  marks: Record<string, 'good' | 'bad'>
  raw?: ProviderRaw[]
  dedupeMerges?: DedupeGroup[]
  scored?: ScoredEntry[]
  dropped?: DroppedEntry[]
  timeline?: JobSearchEvent[]
}

export type MarkRequest = {
  runId: string
  listingId: string
  mark: 'good' | 'bad' | null
}

export type MarkResponse = { success: boolean; error?: string }
export type SaveResponse = { success: boolean; error?: string }

export type DeleteHistoryRequest = { runIds: string[] }
export type DeleteHistoryResponse = { deleted: number; missing: string[]; error?: string }
