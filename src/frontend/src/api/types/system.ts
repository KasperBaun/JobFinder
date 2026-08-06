export type WhoamiResponse = {
  email: string
  dataDir: string
  toolVersion: string
}

export type ImportResponse = { restored: number; skipped: number; warnings: string[] }

export type SetupStatusResponse = {
  configured: boolean
  profileExists: boolean
  email: string | null
  dataDir: string | null
  suggestedEmail: string
  suggestedDataDir: string
  bootstrapPath: string
  /** Persisted interface language; null until the user has made a choice. */
  language: string | null
}

export type SetupRequest = { email: string; dataDir: string; language?: string }

export type SetLanguageRequest = { language: string }

export type LanguageResponse = { language: string }
