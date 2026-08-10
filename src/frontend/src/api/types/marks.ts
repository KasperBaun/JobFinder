export type ApplicationStatus = 'applied' | 'interview' | 'offer' | 'rejected' | 'no-response'

export type MarkRequest = {
  runId: string
  listingId: string
  mark: 'good' | 'bad' | null
  reason?: string | null
}

export type MarkStatusRequest = {
  runId: string
  listingId: string
  status: ApplicationStatus | null
}

export type MarkResponse = { success: boolean; error?: string }

export type ApplicationEntry = {
  listingId: string
  runId: string
  runStartedAt: string
  status: ApplicationStatus
  mark?: 'good' | 'bad'
  reason?: string
  title: string
  company?: string
  location?: string
  url: string
  portal: string
  portalDisplayName?: string
  score: number
  /** ISO timestamp of the last status change; absent for statuses set before R-107. */
  statusChangedAt?: string
}

export type ApplicationsResponse = { applications: ApplicationEntry[] }
