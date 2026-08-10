import type { JobSearch, SearchRequest, StartSearchResponse } from '../types'
import { apiFetch, jsonBody } from './http'

// Enqueue a background search run. Returns immediately with the run id; progress arrives via the SSE
// stream. The run keeps going server-side regardless of this client.
export async function startSearch(req: SearchRequest): Promise<StartSearchResponse> {
  return apiFetch<StartSearchResponse>('/api/search', jsonBody('POST', req))
}

export async function getActiveSearch(): Promise<JobSearch | null> {
  return apiFetch<JobSearch | null>('/api/search/active')
}

export async function getJobSearch(id: string): Promise<JobSearch> {
  return apiFetch<JobSearch>(`/api/search/${encodeURIComponent(id)}`)
}

export async function cancelSearch(id: string): Promise<void> {
  const res = await fetch(`/api/search/${encodeURIComponent(id)}/cancel`, { method: 'POST' })
  if (!res.ok) throw new Error(`Cancel failed: ${res.status}`)
}

// SSE stream of JobSearch snapshots. Each message is the latest full state; the first one is the
// current snapshot (replay-on-connect). Aborting the signal only detaches this viewer — it never
// cancels the background run.
export async function* streamJobSearch(
  id: string,
  signal?: AbortSignal,
): AsyncGenerator<JobSearch> {
  const res = await fetch(`/api/search/${encodeURIComponent(id)}/stream`, {
    method: 'GET',
    headers: { Accept: 'text/event-stream' },
    signal,
  })
  if (!res.ok || !res.body) throw new Error(`Stream failed: ${res.status}`)
  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  while (true) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const events = buffer.split('\n\n')
    buffer = events.pop() ?? ''
    for (const block of events) {
      const dataLine = block.split('\n').find(l => l.startsWith('data: '))
      if (dataLine) yield JSON.parse(dataLine.slice(6)) as JobSearch
    }
  }
}
