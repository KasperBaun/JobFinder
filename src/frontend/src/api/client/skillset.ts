import type { CvExtractionStatus, SaveResponse, SkillsetResponse, SkillsetUpdateRequest } from '../types'
import { apiFetch, jsonBody } from './http'

export async function getSkillset(): Promise<SkillsetResponse> {
  return apiFetch<SkillsetResponse>('/api/skillset')
}

export async function updateSkillset(req: SkillsetUpdateRequest): Promise<SaveResponse> {
  return apiFetch<SaveResponse>('/api/skillset', jsonBody('PUT', req))
}

export type CvExtractionInput = { text?: string; file?: File; url?: string }

// Starts a background CV → profile extraction server-side (30-90s on CPU) and returns
// immediately. Progress is read by polling getCvExtractionStatus(), so the run survives
// navigation and reload — same pattern as the model download.
export async function startCvExtraction(input: CvExtractionInput): Promise<CvExtractionStatus> {
  const form = new FormData()
  if (input.file) form.append('file', input.file)
  if (input.text) form.append('text', input.text)
  if (input.url) form.append('url', input.url)
  // No Content-Type header: the browser sets the multipart boundary.
  const res = await fetch('/api/skillset/extract', { method: 'POST', body: form })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`Extraction failed ${res.status}: ${text}`)
  }
  return res.json() as Promise<CvExtractionStatus>
}

export async function getCvExtractionStatus(): Promise<CvExtractionStatus> {
  return apiFetch<CvExtractionStatus>('/api/skillset/extract/status')
}
