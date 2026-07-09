import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import type { JobSearch, JobSearchEvent, JobSearchPhase } from '../api/types'
import type { SourceCounts } from '../utils/progress'
import { ProgressSummary } from './ProgressSummary'

function ev(phase: JobSearchPhase, iso: string, message?: string): JobSearchEvent {
  return { timestamp: iso, level: 'info', phase, message: message ?? phase }
}

const counts: SourceCounts = { total: 3, ok: 3, failed: 0, running: 0, pending: 0, done: 3 }

function job(overrides: Partial<JobSearch>): JobSearch {
  return {
    id: 'r',
    state: 'succeeded',
    phase: 'done',
    request: {},
    createdAt: '2026-07-08T10:00:00.000Z',
    startedAt: '2026-07-08T10:00:00.000Z',
    finishedAt: '2026-07-08T14:00:20.000Z',
    providers: [],
    fetchedCount: 100,
    dedupedCount: 90,
    rankedCount: 50,
    shortlistCount: 10,
    topScore: 0.8,
    attempt: 2,
    lastHeartbeat: '2026-07-08T14:00:20.000Z',
    timeline: [],
    ...overrides,
  }
}

describe('ProgressSummary', () => {
  it('times the elapsed from the current attempt, not the dead time before a resume', () => {
    // Attempt 1 started at 10:00 and died mid-fetch; attempt 2 resumed 4 hours later and finished 20s
    // later. The elapsed must reflect attempt 2 (0:20), not the 4-hour gap the process was dead.
    const resumed = job({
      currentAttemptStartedAt: '2026-07-08T14:00:00.000Z',
      timeline: [
        ev('fetching', '2026-07-08T10:00:00.000Z', 'Search started'),
        ev('fetching', '2026-07-08T10:00:05.000Z'),
        ev('fetching', '2026-07-08T14:00:00.000Z', 'Search resumed (attempt 2)'),
        ev('done', '2026-07-08T14:00:20.000Z'),
      ],
    })
    render(<ProgressSummary job={resumed} counts={counts} active={false} showDedupe showRank />)
    expect(screen.getByText('0:20')).toBeInTheDocument()
    expect(screen.queryByText('4:00:20')).not.toBeInTheDocument()
  })

  it('falls back to startedAt for a legacy run without the attempt anchor', () => {
    const legacy = job({
      currentAttemptStartedAt: undefined,
      finishedAt: '2026-07-08T10:00:20.000Z',
      lastHeartbeat: '2026-07-08T10:00:20.000Z',
      attempt: 1,
      timeline: [ev('done', '2026-07-08T10:00:20.000Z')],
    })
    render(<ProgressSummary job={legacy} counts={counts} active={false} showDedupe showRank />)
    expect(screen.getByText('0:20')).toBeInTheDocument()
  })
})
