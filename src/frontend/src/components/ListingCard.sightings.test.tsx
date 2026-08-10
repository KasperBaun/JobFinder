import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { ListingMatch } from '../api/types'
import { I18nProvider } from '../i18n'
import { ListingCard } from './ListingCard'

vi.mock('./MarkButton', () => ({ MarkButton: () => null }))
vi.mock('./StatusSelect', () => ({ StatusSelect: () => null }))
vi.mock('./PrintListingButton', () => ({ PrintListingButton: () => null }))

function match(overrides: Partial<ListingMatch>): ListingMatch {
  return {
    id: 'a',
    portal: 'workday-simcorp',
    portalDisplayName: 'SimCorp (Workday)',
    title: 'Senior Software Engineer',
    company: 'SimCorp',
    remoteMode: 'unknown',
    url: 'https://a.com/1',
    score: 0.84,
    reasoning: '',
    primaryStackHits: [],
    secondaryStackHits: [],
    ...overrides,
  }
}

function renderCard(m: ListingMatch) {
  return render(
    <I18nProvider locale="en">
      <ListingCard match={m} runId="run-1" />
    </I18nProvider>,
  )
}

describe('ListingCard sightings', () => {
  it('lists each grouped sighting as a link to the other portal', () => {
    renderCard(match({
      sightings: [{
        id: 'b',
        portal: 'jobindex-rss-csharp',
        portalDisplayName: 'Jobindex (C#)',
        title: 'Senior/Lead Software Engineer',
        url: 'https://jobindex.dk/vis-job/x',
        probability: 0.94,
      }],
    }))

    expect(screen.getByText('Also seen on')).toBeInTheDocument()
    const link = screen.getByRole('link', { name: /Jobindex \(C#\)/ })
    expect(link).toHaveAttribute('href', 'https://jobindex.dk/vis-job/x')
    expect(link).toHaveAttribute('title', 'Senior/Lead Software Engineer')
  })

  it('renders no sightings row when nothing was grouped', () => {
    renderCard(match({}))
    expect(screen.queryByText('Also seen on')).not.toBeInTheDocument()
  })
})
