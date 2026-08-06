import { apiFetch } from './http'

export type LlmDownloadState = 'idle' | 'downloading' | 'completed' | 'failed'

export type LlmDownloadStatus = {
  state: LlmDownloadState
  downloadedBytes: number
  totalBytes: number | null
  error: string | null
}

export type LlmStatus = {
  enabled: boolean
  provider: string
  modelPresent: boolean
  modelPath: string
  modelSizeBytes: number | null
  downloadUrl: string
  download: LlmDownloadStatus
}

export async function getLlmStatus(): Promise<LlmStatus> {
  return apiFetch<LlmStatus>('/api/llm/status')
}

// Kicks off the background download (idempotent — a no-op if one is already running) and returns
// immediately. Progress is not streamed here; the caller polls getLlmStatus() for live state, which
// is what lets the download survive navigation and reload.
export async function startLlmDownload(): Promise<LlmDownloadStatus> {
  return apiFetch<LlmDownloadStatus>('/api/llm/download-model', { method: 'POST' })
}
