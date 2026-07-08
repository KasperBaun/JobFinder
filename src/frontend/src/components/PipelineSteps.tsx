import { useMemo } from 'react'
import { isTerminalState } from '../api/types'
import type { JobSearch, JobSearchEvent } from '../api/types'
import { useElapsed } from '../hooks/useElapsed'
import { finalAttemptStart, phaseDurations } from '../utils/progress'
import { formatStepDuration } from '../utils/time'

type Step = { phase: JobSearch['phase']; label: string }

const STEPS: Step[] = [
  { phase: 'fetching', label: 'Retrieve jobs' },
  { phase: 'deduping', label: 'Deduplicate' },
  { phase: 'ranking', label: 'Rate matches' },
  { phase: 'llmJudging', label: 'AI review' },
  { phase: 'done', label: 'Done' },
]

type Props = {
  phase: JobSearch['phase']
  state: JobSearch['state']
  aiEnabled: boolean
  timeline: JobSearchEvent[]
  startedAt?: string
  finishedAt?: string
}

// Index of the step that is currently active. 'pending' = before the pipeline; 'writing' maps to the
// final "Done" step being in progress; 'done' = past the end (everything complete).
function currentIndex(phase: JobSearch['phase'], steps: Step[]): number {
  if (phase === 'pending') return -1
  if (phase === 'done') return steps.length
  if (phase === 'writing') return steps.length - 1
  return steps.findIndex(s => s.phase === phase)
}

// The numbered pipeline stepper shown at the top of the search card: ① Retrieve jobs → ② Deduplicate
// → … so the user always sees the steps and where the run is. AI review only appears when the local
// model is enabled; a failed/cancelled run marks the step it stopped on. Each completed step shows a
// discrete time spent below it; the active step ticks live; "Done" shows the total run time.
export function PipelineSteps({ phase, state, aiEnabled, timeline, startedAt, finishedAt }: Props) {
  const steps = aiEnabled ? STEPS : STEPS.filter(s => s.phase !== 'llmJudging')
  const failed = state === 'failed' || state === 'cancelled' || state === 'interrupted'
  const current = currentIndex(phase, steps)

  const durations = useMemo(() => phaseDurations(timeline, finishedAt), [timeline, finishedAt])
  // A resumed run's timeline spans every attempt; time only the final one so a restart's dead time
  // isn't counted (see finalAttemptStart). Falls back to the whole timeline / startedAt for legacy runs.
  const attemptStart = useMemo(() => finalAttemptStart(timeline), [timeline])
  const attemptEvents = attemptStart >= 0 ? timeline.slice(attemptStart) : timeline
  const attemptStartIso = attemptStart >= 0 ? timeline[attemptStart].timestamp : startedAt
  const currentStartIso = attemptEvents.find(ev => ev.phase === phase)?.timestamp
  const live = useElapsed(currentStartIso, undefined, !isTerminalState(state))
  const totalMs = attemptStartIso && finishedAt ? Date.parse(finishedAt) - Date.parse(attemptStartIso) : undefined

  return (
    <div className="pipeline" role="list" aria-label="Search steps">
      {steps.map((step, i) => {
        const mod = i < current ? 'done' : i === current ? (failed ? 'failed' : 'current') : 'upcoming'
        const badge = mod === 'done' ? '✓' : mod === 'failed' ? '✕' : i + 1
        let time = ''
        if (mod === 'current' && !failed) time = live
        else if (mod === 'done') {
          time = step.phase === 'done'
            ? (totalMs != null ? formatStepDuration(totalMs) : '')
            : formatStepDuration(durations[step.phase] ?? NaN)
        }
        return (
          <div key={step.phase} className={`pipeline__step pipeline__step--${mod}`} role="listitem">
            <span className="pipeline__num">{badge}</span>
            <span className="pipeline__label">{step.label}</span>
            {time && <span className="pipeline__time">{time}</span>}
          </div>
        )
      })}
    </div>
  )
}
