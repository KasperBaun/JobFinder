export type ProviderType = 'api' | 'rss' | 'html' | 'manual' | 'teamtailor' | 'hrmanager'

export type ProviderSummary = {
  id: number
  name: string
  displayName: string
  type: ProviderType
  enabled: boolean
  endpoint?: string
  rateLimitRps: number
  notes?: string
  /** Danish rendering of `notes`, shipped in the catalog. Absent for user-added sources. */
  notesDa?: string
  lastFetchedAt?: string
  lastFetchCount?: number
  requiresSecret?: string
  hasSecret: boolean
  removable: boolean
}

export type ProvidersResponse = { providers: ProviderSummary[] }

export type ProviderRecentRun = {
  runId: string
  startedAt: string
  status: string
  fetchedCount?: number
  error?: string
}

export type ProviderConfigDefaults = {
  maxPages?: number
  pageSize?: number
  rateLimitRps: number
  enrichBody: boolean
}

export type ProviderConfigView = {
  method?: string
  enrichBody: boolean
  paginates: boolean
  maxPages?: number
  pageSize?: number
  hardCeiling?: number
  searchQuery?: string
  rateLimitRps: number
  defaults: ProviderConfigDefaults
  rateLimitOverridden: boolean
  enrichBodyOverridden: boolean
  maxPagesOverridden: boolean
  pageSizeOverridden: boolean
}

export type ProviderDetail = ProviderSummary & {
  recentRuns: ProviderRecentRun[]
  config: ProviderConfigView
}

export type ProviderEnabledUpdate = { enabled: boolean }

// Per-user override of a source's fetch knobs. Any field omitted/null = keep the catalog default;
// all-empty = reset to defaults.
export type ProviderConfigUpdate = {
  maxPages?: number | null
  pageSize?: number | null
  rateLimitRps?: number | null
  enrichBody?: boolean | null
}

export type SetSecretsRequest = { values: Record<string, string> }

export type ProviderTestSample = {
  title: string
  company?: string
  location?: string
  url: string
}

export type ProviderTestResult = {
  ok: boolean
  fetchedCount: number
  durationMs: number
  sampleTitle?: string
  error?: string
  testedAt: string
  samples: ProviderTestSample[]
  hitPageCap: boolean
  possiblyCapped: boolean
}

// Add-a-source flow. A detected candidate is referenced back to the server by `kind` (+ the pasted
// url) so the client never authors a raw endpoint or field mapping.
export type DetectedSource = {
  kind: string
  displayName: string
  summary: string
  duplicateWarning?: string
}

export type DetectSourceResponse = { candidates: DetectedSource[] }

export type ProviderCreatedResponse = { id: number }
