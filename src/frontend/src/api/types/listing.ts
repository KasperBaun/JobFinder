/** A backend message as a stable key plus the values it interpolates — rendered by the i18n catalog. */
export type ReasoningNote = { key: string; args?: Record<string, unknown> }

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
  /** English prose. Kept as the fallback for runs recorded before `reasoningNotes` existed. */
  reasoning: string
  reasoningNotes?: ReasoningNote[]
  /** The LLM judge's verdict (English by design). Absent when the judge didn't run; runs recorded
   * before the fields existed carry it inside `reasoning` as "AI review: …". */
  llmScore?: number
  llmReason?: string
  primaryStackHits: string[]
  secondaryStackHits: string[]
  favoriteCompany?: boolean
  /** Full fetched ad text. Absent on runs recorded before the field existed (T-009). */
  description?: string
  /** Other portals' copies of this ad, grouped into this slot by the probabilistic matcher
   * (R-117). Absent when nothing was grouped and on runs recorded before the field existed. */
  sightings?: ListingSighting[]
}

/** Another portal's copy of a shortlisted ad, folded into the same slot (R-117). */
export type ListingSighting = {
  id: string
  portal: string
  portalDisplayName?: string
  title: string
  url: string
  probability: number
}

/** A pair the matcher could not settle — probably related, kept as separate listings (R-117). */
export type PossibleDuplicate = {
  keptId: string
  candidateId: string
  probability: number
  /** An employer re-post the same-portal rule refuses to merge. Absent on older runs. */
  samePortal?: boolean
}

export type ScoreBreakdown = {
  primaryStack: number
  secondaryStack: number
  seniority: number
  locationRemote: number
  domain: number
  freshness: number
  disqualifierPenalty: number
  /** Deltas the payload has always carried but the UI ignored; optional for legacy safety. */
  nonEngineeringTitlePenalty?: number
  preferredCompanyBonus?: number
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

