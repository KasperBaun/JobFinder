import type {
  DeleteHistoryResponse,
  DetectSourceResponse,
  HistoryResponse,
  ImportResponse,
  JobSearch,
  MarkRequest,
  MarkResponse,
  ProviderCreatedResponse,
  ProviderDetail,
  ProvidersResponse,
  ProviderTestResult,
  RunDetail,
  SaveResponse,
  SearchRequest,
  SetupRequest,
  SetupStatusResponse,
  SkillsetResponse,
  SkillsetUpdateRequest,
  StartSearchResponse,
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

export async function getSetupStatus(): Promise<SetupStatusResponse> {
  return apiFetch<SetupStatusResponse>('/api/setup/status')
}

export async function completeSetup(req: SetupRequest): Promise<SetupStatusResponse> {
  return apiFetch<SetupStatusResponse>('/api/setup', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
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

export async function getProviders(): Promise<ProvidersResponse> {
  return apiFetch<ProvidersResponse>('/api/providers')
}

export async function getProvider(id: number): Promise<ProviderDetail> {
  return apiFetch<ProviderDetail>(`/api/providers/${id}`)
}

export async function setProviderEnabled(id: number, enabled: boolean): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ enabled }),
  })
}

export async function setProviderSecrets(id: number, values: Record<string, string>): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}/secrets`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ values }),
  })
}

export async function testProvider(id: number): Promise<ProviderTestResult> {
  return apiFetch<ProviderTestResult>(`/api/providers/${id}/test`, { method: 'POST' })
}

export async function deleteProvider(id: number): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}`, { method: 'DELETE' })
}

export async function detectSource(url: string): Promise<DetectSourceResponse> {
  return apiFetch<DetectSourceResponse>('/api/providers/detect', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  })
}

type SourceRef = { url?: string; kind: string; displayName?: string }

export async function previewSource(ref: SourceRef): Promise<ProviderTestResult> {
  return apiFetch<ProviderTestResult>('/api/providers/detect/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(ref),
  })
}

export async function createSource(ref: SourceRef): Promise<ProviderCreatedResponse> {
  return apiFetch<ProviderCreatedResponse>('/api/providers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(ref),
  })
}

export async function getSkillset(): Promise<SkillsetResponse> {
  return apiFetch<SkillsetResponse>('/api/skillset')
}

export async function updateSkillset(req: SkillsetUpdateRequest): Promise<SaveResponse> {
  return apiFetch<SaveResponse>('/api/skillset', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
}

export async function getHistory(): Promise<HistoryResponse> {
  return apiFetch<HistoryResponse>('/api/history')
}

export async function getRun(runId: string): Promise<RunDetail> {
  return apiFetch<RunDetail>(`/api/history/${encodeURIComponent(runId)}`)
}

export async function deleteHistoryRuns(runIds: string[]): Promise<DeleteHistoryResponse> {
  return apiFetch<DeleteHistoryResponse>('/api/history/delete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ runIds }),
  })
}

export async function setMark(req: MarkRequest): Promise<MarkResponse> {
  return apiFetch<MarkResponse>('/api/marks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
}

export type LlmStatus = {
  enabled: boolean
  provider: string
  modelPresent: boolean
  modelPath: string
  modelSizeBytes: number | null
  downloadUrl: string
}

export async function getLlmStatus(): Promise<LlmStatus> {
  return apiFetch<LlmStatus>('/api/llm/status')
}

export type LlmDownloadEvent =
  | { type: 'progress'; downloadedBytes: number; totalBytes: number | null }
  | { type: 'complete'; modelPath: string; bytes: number }
  | { type: 'error'; message: string }

export async function* downloadLlmModel(signal?: AbortSignal): AsyncGenerator<LlmDownloadEvent> {
  const res = await fetch('/api/llm/download-model', {
    method: 'POST',
    headers: { Accept: 'text/event-stream' },
    signal,
  })
  if (!res.ok || !res.body) throw new Error(`Download failed: ${res.status}`)
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
      if (dataLine) yield JSON.parse(dataLine.slice(6)) as LlmDownloadEvent
    }
  }
}

// Enqueue a background search run. Returns immediately with the run id; progress arrives via the SSE
// stream. The run keeps going server-side regardless of this client.
export async function startSearch(req: SearchRequest): Promise<StartSearchResponse> {
  return apiFetch<StartSearchResponse>('/api/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
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
