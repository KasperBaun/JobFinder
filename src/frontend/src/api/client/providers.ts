import type {
  DetectSourceResponse,
  ProviderConfigUpdate,
  ProviderCreatedResponse,
  ProviderDetail,
  ProvidersResponse,
  ProviderTestResult,
  SaveResponse,
  SourcePreviewResult,
} from '../types'
import { apiFetch, jsonBody } from './http'

export async function getProviders(): Promise<ProvidersResponse> {
  return apiFetch<ProvidersResponse>('/api/providers')
}

export async function getProvider(id: number): Promise<ProviderDetail> {
  return apiFetch<ProviderDetail>(`/api/providers/${id}`)
}

export async function setProviderEnabled(id: number, enabled: boolean): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}`, jsonBody('PUT', { enabled }))
}

export async function setProviderSecrets(id: number, values: Record<string, string>): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}/secrets`, jsonBody('PUT', { values }))
}

export async function setProviderConfig(id: number, update: ProviderConfigUpdate): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}/config`, jsonBody('PUT', update))
}

export async function testProvider(id: number): Promise<ProviderTestResult> {
  return apiFetch<ProviderTestResult>(`/api/providers/${id}/test`, { method: 'POST' })
}

export async function deleteProvider(id: number): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}`, { method: 'DELETE' })
}

export async function detectSource(url: string): Promise<DetectSourceResponse> {
  return apiFetch<DetectSourceResponse>('/api/providers/detect', jsonBody('POST', { url }))
}

type SourceRef = { url?: string; kind: string; displayName?: string }

export async function previewSource(ref: SourceRef): Promise<SourcePreviewResult> {
  return apiFetch<SourcePreviewResult>('/api/providers/detect/test', jsonBody('POST', ref))
}

export async function createSource(ref: SourceRef): Promise<ProviderCreatedResponse> {
  return apiFetch<ProviderCreatedResponse>('/api/providers', jsonBody('POST', ref))
}
