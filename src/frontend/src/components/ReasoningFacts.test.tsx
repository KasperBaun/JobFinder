import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ListingMatch, ReasoningNote } from '../api/types'
import { ReasoningFacts } from './ReasoningFacts'

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

describe('ReasoningFacts', () => {
  it('renders every axis as a labelled row, with skills as pills', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET', 'C#'] } },
      { key: 'secondaryHits', args: { skills: ['Docker'] } },
      { key: 'domainHits', args: { domains: ['fintech'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
    ], { remoteMode: 'hybrid' })
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('Must-have')).toBeInTheDocument()
    expect(screen.getByText('.NET')).toBeInTheDocument()
    expect(screen.getByText('C#')).toBeInTheDocument()
    expect(screen.getByText('Nice-to-have')).toBeInTheDocument()
    expect(screen.getByText('Docker')).toBeInTheDocument()
    expect(screen.getByText('fintech')).toBeInTheDocument()
    expect(screen.getByText('Fits')).toBeInTheDocument()
    expect(screen.getByText('Copenhagen')).toBeInTheDocument()
    expect(screen.getByText('hybrid')).toBeInTheDocument()
  })

  it('renders unknown axes muted instead of dropping them', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityUnknown' },
      { key: 'locationRemoteUnknown' },
    ], { location: undefined, remoteMode: 'unknown' })
    render(<ReasoningFacts match={match} />)

    // Experience, Location and Remote all render "Not stated", muted.
    const muted = document.querySelectorAll('.listing-card__fact-value--muted')
    expect(muted).toHaveLength(3)
    expect(screen.getAllByText('Not stated')).toHaveLength(3)
  })

  it('flags a location outside the user’s area', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityClose' },
      { key: 'locationMismatchRemoteUnknown' },
    ], { location: 'Bad Homburg' })
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('Bad Homburg — outside your area')).toBeInTheDocument()
  })

  it('warns when no must-have skills are mentioned', () => {
    const match = makeMatch([{ key: 'noPrimaryHits' }, { key: 'seniorityUnknown' }, { key: 'locationRemoteUnknown' }])
    render(<ReasoningFacts match={match} />)

    const warn = screen.getByText('None of yours mentioned')
    expect(warn.className).toContain('listing-card__fact-value--warn')
  })

  it('short-circuits to a single row for disqualified listings', () => {
    const match = makeMatch([{ key: 'disqualified', args: { hits: ['unpaid', 'agency'] } }])
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('Removed')).toBeInTheDocument()
    expect(screen.getByText('unpaid, agency')).toBeInTheDocument()
    expect(screen.queryByText('Must-have')).not.toBeInTheDocument()
  })

  it('renders the age penalty as its own row', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
      { key: 'agePenalty', args: { days: 41 } },
    ])
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('41 days old — rating reduced')).toBeInTheDocument()
  })

  it('ignores an unknown future note key without crashing', () => {
    const match = makeMatch([
      { key: 'primaryHits', args: { skills: ['.NET'] } },
      { key: 'seniorityMatches' },
      { key: 'location', args: { location: 'Copenhagen' } },
      { key: 'someFutureKey', args: { x: 1 } },
    ])
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('Must-have')).toBeInTheDocument()
  })

  it('shows the AI row from the structured field', () => {
    const match = makeMatch(
      [{ key: 'primaryHits', args: { skills: ['.NET'] } }, { key: 'seniorityMatches' }, { key: 'location', args: { location: 'Copenhagen' } }],
      { llmScore: 0.82, llmReason: 'Strong platform fit for a senior .NET candidate' },
    )
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('AI')).toBeInTheDocument()
    expect(screen.getByText('Strong platform fit for a senior .NET candidate')).toBeInTheDocument()
  })

  it('recovers the AI row from legacy prose when the structured field is absent', () => {
    const match = makeMatch(
      [{ key: 'primaryHits', args: { skills: ['.NET'] } }, { key: 'seniorityMatches' }, { key: 'location', args: { location: 'Copenhagen' } }],
      { reasoning: 'Must-have skill match: .NET. AI review: 0.82 — solid fit, senior role' },
    )
    render(<ReasoningFacts match={match} />)

    expect(screen.getByText('AI')).toBeInTheDocument()
    expect(screen.getByText('solid fit, senior role')).toBeInTheDocument()
  })
})
