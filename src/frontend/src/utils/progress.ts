import type { JobSearch } from '../api/types'

export type ProviderRowState = {
  name: string
  status: 'pending' | 'running' | 'ok' | 'failed'
  fetchedCount?: number
  error?: string
}

const PHASE_ORDER: JobSearch['phase'][] = [
  'pending', 'fetching', 'deduping', 'ranking', 'llmJudging', 'writing', 'done',
]

export function reached(phase: JobSearch['phase'], target: JobSearch['phase']): boolean {
  return PHASE_ORDER.indexOf(phase) >= PHASE_ORDER.indexOf(target)
}

// Merge the enabled-source list (seeded as 'pending') with the live per-source statuses from the
// snapshot, so every source shows a row even before its fetch starts (job.providers starts empty).
export function buildRows(job: JobSearch | null, initialNames: string[]): ProviderRowState[] {
  const rows = new Map<string, ProviderRowState>()
  for (const name of initialNames) rows.set(name, { name, status: 'pending' })
  for (const p of job?.providers ?? []) {
    rows.set(p.name, { name: p.name, status: p.status, fetchedCount: p.fetchedCount, error: p.error })
  }
  return Array.from(rows.values())
}

export type SourceCounts = {
  total: number
  ok: number
  failed: number
  running: number
  pending: number
  done: number
}

export function countSources(rows: ProviderRowState[]): SourceCounts {
  let ok = 0, failed = 0, running = 0, pending = 0
  for (const r of rows) {
    if (r.status === 'ok') ok++
    else if (r.status === 'failed') failed++
    else if (r.status === 'running') running++
    else pending++
  }
  return { total: rows.length, ok, failed, running, pending, done: ok + failed }
}
