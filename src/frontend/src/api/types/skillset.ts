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
  preferredCompanies: string[]
  address?: string | null
  radiusKm?: number | null
  /** Server-computed at save time (DAWA geocoding) — never sent by the client. */
  latitude?: number | null
  longitude?: number | null
  resolvedAddress?: string | null
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
  preferredCompanies: string[]
  address?: string | null
  radiusKm?: number | null
}

export type CvExtractionState = 'idle' | 'extracting' | 'completed' | 'failed'

// All fields optional: the extractor only reports what the CV states, and the
// server omits nulls from the JSON.
export type ExtractedProfile = {
  name?: string | null
  location?: string | null
  country?: string | null
  region?: string | null
  metro?: string[]
  experienceYears?: number | null
  seniority?: string | null
  remotePreference?: string | null
  targetRoles?: string[]
  primaryStack?: string[]
  secondaryStack?: string[]
  domains?: string[]
  languages?: string[]
  employmentTypes?: string[]
}

export type CvExtractionStatus = {
  state: CvExtractionState
  startedAt?: string | null
  error?: string | null
  profile?: ExtractedProfile | null
}
