import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ApplicationsPage } from './ApplicationsPage'
import * as client from '../api/client'
import type { ApplicationEntry } from '../api/types'

vi.mock('../api/client')

const DAY_MS = 86_400_000
const daysAgo = (days: number) => new Date(Date.now() - days * DAY_MS).toISOString()

function entry(over: Partial<ApplicationEntry> & Pick<ApplicationEntry, 'listingId' | 'title' | 'status'>): ApplicationEntry {
  return {
    runId: '20260701-100000-aaaaaa',
    runStartedAt: daysAgo(30),
    url: `https://x/${over.listingId}`,
    portal: 'portal',
    score: 0.8,
    ...over,
  }
}

const APPS: ApplicationEntry[] = [
  entry({ listingId: 'o1', title: 'Offer Co', status: 'offer' }),
  entry({ listingId: 'i1', title: 'Interview Co', status: 'interview', statusChangedAt: daysAgo(2) }),
  entry({ listingId: 'a1', title: 'Stale Applied', status: 'applied', statusChangedAt: daysAgo(20) }),
  entry({ listingId: 'a2', title: 'Fresh Applied', status: 'applied', statusChangedAt: daysAgo(5) }),
]

function renderPage() {
  vi.mocked(client.getApplications).mockResolvedValue({ applications: APPS })
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  })
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <ApplicationsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const bodyTitles = () =>
  screen.getAllByRole('row').slice(1).map((r) => within(r).getAllByRole('cell')[0].textContent)

describe('ApplicationsPage', () => {
  beforeEach(() => vi.resetAllMocks())

  it('shows per-status stat tiles with counts', async () => {
    renderPage()
    const tiles = await screen.findByRole('group', { name: /applications by status/i })
    expect(within(tiles).getByRole('button', { name: /4 all/i })).toBeInTheDocument()
    expect(within(tiles).getByRole('button', { name: /2 applied/i })).toBeInTheDocument()
    expect(within(tiles).getByRole('button', { name: /1 interview/i })).toBeInTheDocument()
    expect(within(tiles).getByRole('button', { name: /1 offer/i })).toBeInTheDocument()
    expect(within(tiles).queryByRole('button', { name: /rejected/i })).not.toBeInTheDocument()
  })

  it('filters by status when a tile is clicked, and clears on the second click', async () => {
    renderPage()
    const applied = await screen.findByRole('button', { name: /2 applied/i })

    await userEvent.click(applied)
    expect(bodyTitles()).toEqual(['Stale Applied', 'Fresh Applied'])
    expect(applied).toHaveAttribute('aria-pressed', 'true')

    await userEvent.click(applied)
    expect(bodyTitles()).toHaveLength(4)
  })

  it('badges applied listings older than the awaiting-response threshold only', async () => {
    renderPage()
    await screen.findByText('Stale Applied')

    expect(screen.getByText('waiting 20 days')).toBeInTheDocument()
    // Fresh applied (5 days) and non-applied rows must not be badged.
    expect(screen.getAllByText(/^waiting/)).toHaveLength(1)
  })

  it('renders the status-set column through the locale relative formatter', async () => {
    renderPage()
    await screen.findByText('Stale Applied')

    expect(screen.getByText('20 days ago')).toBeInTheDocument()
    expect(screen.getByText('2 days ago')).toBeInTheDocument()
  })

  it('sorts by status-set date, keeping timestampless entries last', async () => {
    renderPage()
    await screen.findByText('Stale Applied')
    const header = screen.getByRole('columnheader', { name: /status set/i })

    await userEvent.click(header)
    expect(header).toHaveAttribute('aria-sort', 'descending')
    expect(bodyTitles()).toEqual(['Interview Co', 'Fresh Applied', 'Stale Applied', 'Offer Co'])

    await userEvent.click(header)
    expect(header).toHaveAttribute('aria-sort', 'ascending')
    expect(bodyTitles()).toEqual(['Stale Applied', 'Fresh Applied', 'Interview Co', 'Offer Co'])

    // Third click restores the server's activity ordering.
    await userEvent.click(header)
    expect(bodyTitles()).toEqual(['Offer Co', 'Interview Co', 'Stale Applied', 'Fresh Applied'])
  })
})
