import type { WhoamiResponse } from './types'

async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(path, options)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`API error ${res.status}: ${text}`)
  }
  return res.json() as Promise<T>
}

export async function getWhoami(): Promise<WhoamiResponse> {
  return apiFetch<WhoamiResponse>('/api/whoami')
}

export async function shutdown(): Promise<void> {
  await fetch('/api/shutdown', { method: 'POST' })
}

export async function ping(): Promise<void> {
  const res = await fetch('/api/ping', { method: 'GET', cache: 'no-store' })
  if (!res.ok) {
    throw new Error(`API error ${res.status}`)
  }
}
