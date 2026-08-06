import type {
  ApplicationsResponse,
  DeleteHistoryResponse,
  HistoryResponse,
  MarkRequest,
  MarkResponse,
  MarkStatusRequest,
  RunDetail,
} from '../types'
import { apiFetch, jsonBody } from './http'

export async function getHistory(): Promise<HistoryResponse> {
  return apiFetch<HistoryResponse>('/api/history')
}

export async function getRun(runId: string): Promise<RunDetail> {
  return apiFetch<RunDetail>(`/api/history/${encodeURIComponent(runId)}`)
}

export async function deleteHistoryRuns(runIds: string[]): Promise<DeleteHistoryResponse> {
  return apiFetch<DeleteHistoryResponse>('/api/history/delete', jsonBody('POST', { runIds }))
}

export async function setMark(req: MarkRequest): Promise<MarkResponse> {
  return apiFetch<MarkResponse>('/api/marks', jsonBody('POST', req))
}

export async function setMarkStatus(req: MarkStatusRequest): Promise<MarkResponse> {
  return apiFetch<MarkResponse>('/api/marks/status', jsonBody('POST', req))
}

export async function getApplications(): Promise<ApplicationsResponse> {
  return apiFetch<ApplicationsResponse>('/api/applications')
}
