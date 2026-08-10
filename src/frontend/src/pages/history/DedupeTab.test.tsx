import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { RunDetail } from '../../api/types'
import { I18nProvider } from '../../i18n'
import { DedupeTab } from './DedupeTab'

function detail(overrides: Partial<RunDetail>): RunDetail {
  return {
    runId: 'run-1',
    startedAt: '2026-08-06T11:32:47Z',
    providers: [],
    fetchedCount: 3,
    dedupedCount: 2,
    rankedCount: 2,
    shortlistCount: 2,
    topScore: 0.9,
    goodMarks: 0,
    shortlist: [],
    marks: {},
    scored: [
      { id: 'a', title: 'Senior Engineer', url: 'https://a.com/1', portal: 'oracle', portalDisplayName: 'Oracle', score: 0.9, breakdown: { primaryStack: 0, secondaryStack: 0, seniority: 0, locationRemote: 0, domain: 0, freshness: 0, disqualifierPenalty: 0 }, primaryStackHits: [], secondaryStackHits: [] },
      { id: 'b', title: 'Senior Engineer (jobindex)', url: 'https://b.com/1', portal: 'jobindex', portalDisplayName: 'Jobindex', score: 0.85, breakdown: { primaryStack: 0, secondaryStack: 0, seniority: 0, locationRemote: 0, domain: 0, freshness: 0, disqualifierPenalty: 0 }, primaryStackHits: [], secondaryStackHits: [] },
      { id: 'c', title: 'Platform Engineer', url: 'https://c.com/1', portal: 'workday', portalDisplayName: 'Workday', score: 0.8, breakdown: { primaryStack: 0, secondaryStack: 0, seniority: 0, locationRemote: 0, domain: 0, freshness: 0, disqualifierPenalty: 0 }, primaryStackHits: [], secondaryStackHits: [] },
    ],
    ...overrides,
  }
}

function renderTab(data: RunDetail) {
  return render(
    <I18nProvider locale="en">
      <DedupeTab data={data} />
    </I18nProvider>,
  )
}

describe('DedupeTab possible duplicates', () => {
  it('renders the possible-duplicates section with titles, sources and probability', () => {
    renderTab(detail({
      dedupeMerges: [],
      possibleDuplicates: [{ keptId: 'a', candidateId: 'c', probability: 0.33 }],
    }))

    expect(screen.getByText('1 possible duplicate')).toBeInTheDocument()
    expect(screen.getByText('Senior Engineer')).toBeInTheDocument()
    expect(screen.getByText('Platform Engineer')).toBeInTheDocument()
    expect(screen.getByText('Oracle')).toBeInTheDocument()
    expect(screen.getByText('Workday')).toBeInTheDocument()
    expect(screen.getByText('probability 0.33')).toBeInTheDocument()
  })

  it('shows the possible section even when no exact merges happened', () => {
    renderTab(detail({
      dedupeMerges: [],
      possibleDuplicates: [{ keptId: 'a', candidateId: 'b', probability: 0.5 }],
    }))

    expect(screen.queryByText('No duplicates were merged in this search.')).not.toBeInTheDocument()
    expect(screen.getByText('1 possible duplicate')).toBeInTheDocument()
  })

  it('keeps the empty message when there are neither merges nor possible pairs', () => {
    renderTab(detail({ dedupeMerges: [], possibleDuplicates: [] }))
    expect(screen.getByText(/no duplicates were merged/i)).toBeInTheDocument()
  })

  it('previews long possible lists and expands on demand', async () => {
    const many = Array.from({ length: 45 }, (_, i) => ({
      keptId: 'a',
      candidateId: 'b',
      probability: 0.9 - i * 0.001,
    }))
    renderTab(detail({ dedupeMerges: [], possibleDuplicates: many }))

    expect(screen.getAllByText(/probability/)).toHaveLength(30)
    fireEvent.click(screen.getByRole('button', { name: 'Show all 45' }))
    expect(await screen.findAllByText(/probability/)).toHaveLength(45)
  })

  it('renders exact merges alongside possible pairs', () => {
    renderTab(detail({
      dedupeMerges: [{ canonicalId: 'a', mergedFromIds: ['b'] }],
      possibleDuplicates: [{ keptId: 'a', candidateId: 'c', probability: 0.4 }],
    }))

    expect(screen.getByText('kept')).toBeInTheDocument()
    expect(screen.getByText('also seen in 1 other place')).toBeInTheDocument()
    expect(screen.getByText('1 possible duplicate')).toBeInTheDocument()
  })
})
