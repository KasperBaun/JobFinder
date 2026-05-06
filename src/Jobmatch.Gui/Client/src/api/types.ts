export type WhoamiResponse = {
  email: string
  dataDir: string
  toolVersion: string
}

export type ProviderType = 'api' | 'rss' | 'html' | 'manual'

export type ProviderSummary = {
  name: string
  type: ProviderType
  enabled: boolean
  baseUrl?: string
  endpoint?: string
  rateLimitRps: number
  notes?: string
  lastFetchedAt?: string
  lastFetchCount?: number
}

export type ProvidersResponse = { providers: ProviderSummary[] }

export type ProviderUpsert = {
  name: string
  type: ProviderType
  enabled: boolean
  baseUrl?: string
  endpoint?: string
  rateLimitRps: number
  notes?: string
}

export type ProvidersUpdateRequest = { providers: ProviderUpsert[] }

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

export type RunDetail = RunSummary & {
  shortlist: ListingMatch[]
  marks: Record<string, 'good' | 'bad'>
}

export type MarkRequest = {
  runId: string
  listingId: string
  mark: 'good' | 'bad' | null
}

export type MarkResponse = { success: boolean; error?: string }
export type SaveResponse = { success: boolean; error?: string }
