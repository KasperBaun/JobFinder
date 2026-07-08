import type { JobSearch, JobSearchEvent } from '../api/types'

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

// Events belonging to the current run attempt. A run interrupted by a host restart resumes by re-driving
// the whole pipeline, so its timeline concatenates every attempt separated by the (possibly hours-long)
// dead time between them. The backend stamps each attempt's start on currentAttemptStartedAt; slicing the
// timeline at it keeps timings from folding in the time the process was down. Returns the whole timeline
// when the anchor is absent (legacy runs) or unparseable.
export function attemptEvents(timeline: JobSearchEvent[], attemptStartIso?: string): JobSearchEvent[] {
  if (!attemptStartIso) return timeline
  const start = Date.parse(attemptStartIso)
  if (Number.isNaN(start)) return timeline
  return timeline.filter(ev => Date.parse(ev.timestamp) >= start)
}

// Per-phase durations (ms), derived from the timeline: every phase transition writes a timestamped
// event, so a phase's span is (start of the next phase that occurred − its own first timestamp). The
// last occurring phase is closed by finishedAt. Keyed by phase; 'pending' is ignored, and the display
// 'done' value (total run time) is computed in the component, not here. Pass a single attempt's events
// (see attemptEvents) so a resumed run doesn't fold the dead time between attempts into the spans.
export function phaseDurations(
  timeline: JobSearchEvent[],
  finishedAt?: string,
): Partial<Record<JobSearch['phase'], number>> {
  const firstTs = new Map<JobSearch['phase'], number>()
  for (const ev of timeline) {
    if (ev.phase === 'pending') continue
    const t = Date.parse(ev.timestamp)
    if (Number.isNaN(t) || firstTs.has(ev.phase)) continue
    firstTs.set(ev.phase, t)
  }

  const finished = finishedAt ? Date.parse(finishedAt) : NaN
  const occurring = PHASE_ORDER.filter(p => p !== 'pending' && firstTs.has(p))
  const out: Partial<Record<JobSearch['phase'], number>> = {}
  for (let i = 0; i < occurring.length; i++) {
    const start = firstTs.get(occurring[i])!
    const end = i + 1 < occurring.length ? firstTs.get(occurring[i + 1])! : finished
    if (!Number.isNaN(end) && end >= start) out[occurring[i]] = end - start
  }
  return out
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
