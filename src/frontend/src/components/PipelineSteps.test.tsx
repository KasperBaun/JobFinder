import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import type { JobSearch, JobSearchEvent } from '../api/types'
import { PipelineSteps } from './PipelineSteps'

function ev(phase: JobSearch['phase'], iso: string): JobSearchEvent {
  return { timestamp: iso, level: 'info', phase, message: phase }
}

const completedTimeline = [
  ev('fetching', '2026-07-08T10:00:00.000Z'),
  ev('deduping', '2026-07-08T10:00:03.000Z'),
  ev('ranking', '2026-07-08T10:00:04.000Z'),
  ev('llmJudging', '2026-07-08T10:00:16.000Z'),
  ev('done', '2026-07-08T10:00:20.000Z'),
]

describe('PipelineSteps', () => {
  it('shows a per-step duration for completed steps and total run time under Done', () => {
    render(
      <PipelineSteps
        phase="done"
        state="succeeded"
        aiEnabled
        timeline={completedTimeline}
        startedAt="2026-07-08T10:00:00.000Z"
        finishedAt="2026-07-08T10:01:04.000Z"
      />,
    )
    expect(screen.getByText('3.0s')).toBeInTheDocument() // fetching
    expect(screen.getByText('12.0s')).toBeInTheDocument() // ranking
    expect(screen.getByText('4.0s')).toBeInTheDocument() // llmJudging
    expect(screen.getByText('1m 04s')).toBeInTheDocument() // Done → total run time
  })

  it('drops the AI review step and its timing when the model is disabled', () => {
    render(
      <PipelineSteps
        phase="done"
        state="succeeded"
        aiEnabled={false}
        timeline={completedTimeline}
        startedAt="2026-07-08T10:00:00.000Z"
        finishedAt="2026-07-08T10:01:04.000Z"
      />,
    )
    expect(screen.queryByText('AI review')).not.toBeInTheDocument()
  })

  it('shows no time under upcoming steps mid-run', () => {
    render(
      <PipelineSteps
        phase="deduping"
        state="running"
        aiEnabled
        timeline={[ev('fetching', '2026-07-08T10:00:00.000Z'), ev('deduping', '2026-07-08T10:00:03.000Z')]}
        startedAt="2026-07-08T10:00:00.000Z"
      />,
    )
    // Completed 'fetching' shows its duration; the not-yet-reached 'ranking' shows nothing.
    expect(screen.getByText('3.0s')).toBeInTheDocument()
    expect(screen.getByText('Rate matches').parentElement?.querySelector('.pipeline__time')).toBeNull()
  })
})
