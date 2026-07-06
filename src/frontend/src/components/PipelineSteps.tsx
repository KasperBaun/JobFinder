import type { JobSearch } from '../api/types'

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
// model is enabled; a failed/cancelled run marks the step it stopped on.
export function PipelineSteps({ phase, state, aiEnabled }: Props) {
  const steps = aiEnabled ? STEPS : STEPS.filter(s => s.phase !== 'llmJudging')
  const failed = state === 'failed' || state === 'cancelled' || state === 'interrupted'
  const current = currentIndex(phase, steps)

  return (
    <div className="pipeline" role="list" aria-label="Search steps">
      {steps.map((step, i) => {
        const mod = i < current ? 'done' : i === current ? (failed ? 'failed' : 'current') : 'upcoming'
        const badge = mod === 'done' ? '✓' : mod === 'failed' ? '✕' : i + 1
        return (
          <div key={step.phase} className={`pipeline__step pipeline__step--${mod}`} role="listitem">
            <span className="pipeline__num">{badge}</span>
            <span className="pipeline__label">{step.label}</span>
          </div>
        )
      })}
    </div>
  )
}
