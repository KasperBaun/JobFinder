import type {
  HistoryResponse,
  MarkRequest,
  MarkResponse,
  ProvidersResponse,
  RunDetail,
  SearchProgressEvent,
  SearchRequest,
  SkillsetResponse,
  WhoamiResponse,
} from './types'

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

export async function getProviders(): Promise<ProvidersResponse> {
  return apiFetch<ProvidersResponse>('/api/providers')
}

export async function getSkillset(): Promise<SkillsetResponse> {
  return apiFetch<SkillsetResponse>('/api/skillset')
}

export async function getHistory(): Promise<HistoryResponse> {
  return apiFetch<HistoryResponse>('/api/history')
}

export async function getRun(runId: string): Promise<RunDetail> {
  return apiFetch<RunDetail>(`/api/history/${encodeURIComponent(runId)}`)
}

export async function setMark(req: MarkRequest): Promise<MarkResponse> {
  return apiFetch<MarkResponse>('/api/marks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
}

export async function* startSearch(
  req: SearchRequest,
  signal?: AbortSignal,
): AsyncGenerator<SearchProgressEvent> {
  const res = await fetch('/api/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Accept': 'text/event-stream' },
    body: JSON.stringify(req),
    signal,
  })
  if (!res.ok || !res.body) throw new Error(`Search failed: ${res.status}`)
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
      if (dataLine) yield JSON.parse(dataLine.slice(6)) as SearchProgressEvent
    }
  }
}
