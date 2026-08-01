import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ListingMatch, ReasoningNote } from '../api/types'
import { AiVerdict, ReasoningFacts } from './ReasoningFacts'

vi.mock('../api/client', () => ({
  getSkillset: vi.fn(() => Promise.resolve({ primaryStack: ['.NET', 'Azure', 'React'] })),
}))

function makeMatch(notes: ReasoningNote[], overrides: Partial<ListingMatch> = {}): ListingMatch {
  return {
    id: 'l1',
    portal: 'test',
    title: 'Senior .NET Engineer',
    company: 'Acme',
    location: 'Copenhagen',
    remoteMode: 'unknown',
    url: 'https://acme.com/jobs/1',
    score: 0.8,
    reasoning: '',
    reasoningNotes: notes,
    primaryStackHits: [],
    secondaryStackHits: [],
    ...overrides,
  }
}

function renderFacts(match: ListingMatch) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ReasoningFacts match={match} />
    </QueryClientProvider>,
  )
}

const daysAgo = (days: number) => new Date(Date.now() - days * 86_400_000).toISOString()

describe('ReasoningFacts', () => {
  it('renders known axes as labelled rows with hit pills, good tones and a posted date', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'secondaryHits', args: { skills: ['Docker'] } },
      { key: 'domainHits', args: { domains: ['fintech'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
    ], { remoteMode: 'hybrid', postedAt: daysAgo(11) })
    renderFacts(match)

    expect(screen.getByText('.NET')).toBeInTheDocument()
    expect(screen.getByText('Docker')).toBeInTheDocument()
    expect(screen.getByText('fintech')).toBeInTheDocument()
    expect(screen.getByText('✓ Fits').className).toContain('--good')
    expect(screen.getByText('✓ Copenhagen').className).toContain('--good')
    expect(screen.getByText('hybrid')).toBeInTheDocument()
    expect(screen.getByText('Posted')).toBeInTheDocument()
    expect(screen.getByText('11 days ago')).toBeInTheDocument()
  })

  it('shows unmatched must-haves as ghost pills once the skillset loads', async () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
    ])
    renderFacts(match)

    await waitFor(() => expect(screen.getByText('Azure')).toBeInTheDocument())
    expect(screen.getByText('Azure').className).toContain('pill--ghost')
    expect(screen.getByText('React').className).toContain('pill--ghost')
    expect(screen.getByText('.NET').className).toContain('pill--primary')
  })

  it('folds two or more unknown axes into a single muted row', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityUnknown' },
      { key: 'locationRemoteUnknown' },
    ], { location: undefined, remoteMode: 'unknown', postedAt: undefined })
    renderFacts(match)

    expect(screen.getByText('Experience · Location · Remote · Posted')).toBeInTheDocument()
    expect(document.querySelectorAll('.listing-card__fact-value--muted')).toHaveLength(1)
  })

  it('renders a single unknown axis in place instead of folding', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
    ], { remoteMode: 'unknown', postedAt: daysAgo(3) })
    renderFacts(match)

    expect(screen.getByText('Remote')).toBeInTheDocument()
    expect(screen.getByText('Not stated').className).toContain('--muted')
  })

  it('flags a location outside the user’s area and an age-reduced posting', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityClose' },
      { key: 'locationMismatchRemoteUnknown' },
      { key: 'agePenalty', args: { days: 145 } },
    ], { location: 'Bad Homburg', postedAt: daysAgo(145) })
    renderFacts(match)

    expect(screen.getByText('Bad Homburg — outside your area').className).toContain('--warn')
    expect(screen.getByText(/rating reduced for age/).className).toContain('--warn')
  })

  it('warns when no must-have skills are mentioned', () => {
    const match = makeMatch([{ key: 'noPrimaryHits' }, { key: 'seniorityUnknown' }, { key: 'locationRemoteUnknown' }])
    renderFacts(match)

    expect(screen.getByText(/None of yours mentioned/).closest('dd')!.className).toContain('--warn')
  })

  it('short-circuits to a single row for disqualified listings', () => {
    const match = makeMatch([{ key: 'disqualified', args: { hits: ['unpaid', 'agency'] } }])
    renderFacts(match)

    expect(screen.getByText('Removed')).toBeInTheDocument()
    expect(screen.getByText('unpaid, agency')).toBeInTheDocument()
    expect(screen.queryByText('Must-have')).not.toBeInTheDocument()
  })

  it('ignores an unknown future note key without crashing', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
      { key: 'someFutureKey', args: { x: 1 } },
    ])
    renderFacts(match)

    expect(screen.getByText('Must-have')).toBeInTheDocument()
  })
})

describe('AiVerdict', () => {
  it('shows the structured verdict with its score and expands on click', async () => {
    const match = makeMatch([], { llmScore: 0.82, llmReason: 'Strong platform fit for a senior .NET candidate' })
    render(<AiVerdict match={match} />)

    const strip = screen.getByRole('button')
    expect(strip.textContent).toContain('AI 0.82')
    expect(strip.textContent).toContain('Strong platform fit')
    await userEvent.click(strip)
    expect(strip.className).toContain('--expanded')
  })

  it('recovers score and reason from legacy prose', () => {
    const match = makeMatch([], { reasoning: 'Must-have skill match: .NET. AI review: 0.90 — solid fit, senior role' })
    render(<AiVerdict match={match} />)

    expect(screen.getByRole('button').textContent).toContain('AI 0.90')
    expect(screen.getByText(/solid fit, senior role/)).toBeInTheDocument()
  })

  it('says so when the judge never reviewed the listing', () => {
    const match = makeMatch([])
    render(<AiVerdict match={match} />)

    expect(screen.getByText('Not reviewed by AI')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
