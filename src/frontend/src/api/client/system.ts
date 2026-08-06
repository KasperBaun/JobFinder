import type {
  ImportResponse,
  LanguageResponse,
  SetLanguageRequest,
  SetupRequest,
  SetupStatusResponse,
  WhoamiResponse,
} from '../types'
import { apiFetch, jsonBody } from './http'

export async function getWhoami(): Promise<WhoamiResponse> {
  return apiFetch<WhoamiResponse>('/api/whoami')
}

export async function getSetupStatus(): Promise<SetupStatusResponse> {
  return apiFetch<SetupStatusResponse>('/api/setup/status')
}

export async function completeSetup(req: SetupRequest): Promise<SetupStatusResponse> {
  return apiFetch<SetupStatusResponse>('/api/setup', jsonBody('POST', req))
}

export async function setLanguage(language: string): Promise<LanguageResponse> {
  return apiFetch<LanguageResponse>(
    '/api/settings/language',
    jsonBody('PUT', { language } satisfies SetLanguageRequest),
  )
}

export async function shutdown(): Promise<void> {
  await fetch('/api/system/shutdown', { method: 'POST' })
}

export async function ping(): Promise<void> {
  const res = await fetch('/api/system/ping', { method: 'GET', cache: 'no-store' })
  if (!res.ok) {
    throw new Error(`API error ${res.status}`)
  }
}

export async function exportConfig(): Promise<void> {
  const res = await fetch('/api/config/export')
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`Export failed ${res.status}: ${text}`)
  }
  const blob = await res.blob()
  const disposition = res.headers.get('Content-Disposition') ?? ''
  const match = /filename="?([^"]+)"?/i.exec(disposition)
  const fileName = match?.[1] ?? 'jobfinder-export.zip'

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

export async function importConfig(file: File): Promise<ImportResponse> {
  const form = new FormData()
  form.append('file', file)
  // No Content-Type header: the browser sets the multipart boundary.
  const res = await fetch('/api/config/import', { method: 'POST', body: form })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`Import failed ${res.status}: ${text}`)
  }
  return res.json() as Promise<ImportResponse>
}
