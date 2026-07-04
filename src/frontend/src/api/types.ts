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

export type SearchProgressEvent =
  | { type: 'started'; total: number }
  | { type: 'provider_running'; provider: string; index: number; total: number }
  | { type: 'provider_done'; provider: string; fetchedCount: number; index: number; total: number }
  | { type: 'provider_failed'; provider: string; error: string; index: number; total: number }
  | { type: 'dedupe'; mergedCount: number }
  | { type: 'rank'; rankedCount: number; topScore: number }
  | { type: 'complete'; runId: string; shortlist: ListingMatch[] }
  | { type: 'error'; message: string }

export type ProviderRunStatus = {
  name: string
  status: 'ok' | 'failed'
  fetchedCount?: number
  error?: string
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

export type ImportResponse = { restored: number; skipped: number; warnings: string[] }

export type SetupStatusResponse = {
  configured: boolean
  profileExists: boolean
  email: string | null
  dataDir: string | null
  suggestedEmail: string
  suggestedDataDir: string
  bootstrapPath: string
}

export type SetupRequest = { email: string; dataDir: string }
