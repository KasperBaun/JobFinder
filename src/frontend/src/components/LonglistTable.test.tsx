import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { RunDetail, ScoredEntry } from '../api/types'
import { I18nProvider } from '../i18n'
import type { Locale } from '../i18n'
import { LonglistTable } from './LonglistTable'
import { DEFAULT_FILTERS, type LonglistFilters } from './longlist/filterState'
import { DEFAULT_SORT, type LonglistSort } from './longlist/sortState'

vi.mock('../api/client', () => ({
  setMark: vi.fn(() => Promise.resolve({ success: true })),
  setMarkStatus: vi.fn(() => Promise.resolve({ success: true })),
}))

function scored(over: Partial<ScoredEntry> & { id: string; title: string }): ScoredEntry {
  return {
    url: `https://example.test/${over.id}`,
    portal: 'itjobbank',
    score: 0.5,
    breakdown: {
      primaryStack: 0, secondaryStack: 0, seniority: 0,
      locationRemote: 0, domain: 0, freshness: 0, disqualifierPenalty: 0,
    },
    primaryStackHits: [],
    secondaryStackHits: [],
    ...over,
  }
}

const ROWS: ScoredEntry[] = [
  scored({ id: 'a', title: 'Alpha Engineer', company: 'Zeta', score: 0.20, postedAt: '2026-07-01T00:00:00Z' }),
  scored({ id: 'b', title: 'Bravo Engineer', company: 'Acme', score: 0.90 }),
  scored({ id: 'c', title: 'Charlie Engineer', company: 'Mid', score: 0.50, postedAt: '2026-08-01T00:00:00Z' }),
]

function runDetail(over: Partial<RunDetail> = {}): RunDetail {
  return {
    runId: 'run-1',
    startedAt: '2026-08-01T00:00:00Z',
    shortlist: [],
    marks: {},
    scored: ROWS,
    ...over,
  } as RunDetail
}

function renderTable({
  data = runDetail(),
  filters = DEFAULT_FILTERS,
  sort = DEFAULT_SORT,
  locale = 'en' as Locale,
}: {
  data?: RunDetail
  filters?: LonglistFilters
  sort?: LonglistSort
  locale?: Locale
} = {}) {
  const onFiltersChange = vi.fn()
  const onSortChange = vi.fn()
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <I18nProvider locale={locale}>
      <QueryClientProvider client={client}>
        <LonglistTable
          data={data}
          filters={filters}
          sort={sort}
          onFiltersChange={onFiltersChange}
          onSortChange={onSortChange}
          shortlistIds={new Set(data.shortlist.map((m) => m.id))}
        />
      </QueryClientProvider>
    </I18nProvider>,
  )
  return { onFiltersChange, onSortChange }
}

/** Row titles in render order — the actual proof that a sort took effect. */
function bodyTitles(): string[] {
  const rows = screen.getAllByRole('row').slice(1) // drop the header row
  return rows
    .map((r) => r.querySelector('td a')?.textContent ?? '')
    .filter(Boolean)
}

function header(name: RegExp) {
  return screen.getByRole('columnheader', { name })
}

function headerControl(name: RegExp) {
  return within(header(name)).getByRole('button')
}

describe('LonglistTable', () => {
  it('renders nothing but a notice when the run recorded no ratings', () => {
    renderTable({ data: runDetail({ scored: undefined }) })
    expect(screen.getByText(/no ratings recorded/i)).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('orders by rating descending by default', () => {
    renderTable()
    expect(bodyTitles()).toEqual(['Bravo Engineer', 'Charlie Engineer', 'Alpha Engineer'])
  })

  describe('sortable headers', () => {
    it('marks exactly one column as sorted, and it is the active one', () => {
      renderTable()
      const sorted = screen.getAllByRole('columnheader').filter((h) => h.hasAttribute('aria-sort'))
      expect(sorted).toHaveLength(1)
      expect(sorted[0]).toHaveAttribute('aria-sort', 'descending')
      expect(sorted[0]).toHaveTextContent(/rating/i)
    })

    it('exposes every sortable column through a focusable control', () => {
      renderTable()
      for (const name of [/title/i, /company/i, /source/i, /location/i, /posted/i, /^rating/i, /your rating/i]) {
        expect(headerControl(name)).toBeInstanceOf(HTMLButtonElement)
      }
    })

    it('is reachable by keyboard alone', async () => {
      const { onSortChange } = renderTable()
      const control = headerControl(/title/i)
      control.focus()
      expect(control).toHaveFocus()
      await userEvent.keyboard('{Enter}')
      expect(onSortChange).toHaveBeenCalledWith({ key: 'title', dir: 'asc' })

      await userEvent.keyboard(' ')
      expect(onSortChange).toHaveBeenCalledTimes(2)
    })

    it('starts a text column ascending and the rating column descending', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'title', dir: 'asc' } })
      await userEvent.click(headerControl(/company/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'company', dir: 'asc' })

      await userEvent.click(headerControl(/^rating/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'desc' })
    })

    it('reverses the direction when the active column is clicked again', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'score', dir: 'desc' } })
      await userEvent.click(headerControl(/^rating/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'asc' })
    })

    it('never asks for an unsorted third state', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'title', dir: 'desc' } })
      await userEvent.click(headerControl(/title/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'title', dir: 'asc' })
    })

    it('applies the sort it is given', () => {
      // Deliberately a key whose order differs from the rating-descending default, so passing
      // proves the sort ran rather than that nothing happened.
      renderTable({ sort: { key: 'title', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Bravo Engineer', 'Charlie Engineer'])
    })

    it('puts listings with no posting date last', () => {
      renderTable({ sort: { key: 'posted', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Charlie Engineer', 'Bravo Engineer'])
    })

    it('sorts by your rating, which lives on the run rather than on the listing', () => {
      // Proves the marks recorded against the run reach the comparator: 'a' has the lowest score,
      // so rating-descending would put it last.
      const data = runDetail({ marks: { a: 'good', b: 'bad' } })
      renderTable({ data, sort: { key: 'mark', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Charlie Engineer', 'Bravo Engineer'])
      expect(header(/your rating/i)).toHaveAttribute('aria-sort', 'ascending')
    })
  })

  describe('sort bar', () => {
    it('states the visible count and total', () => {
      renderTable()
      expect(screen.getByText('3 jobs')).toBeInTheDocument()
    })

    it('reports a filtered subset against the full run', () => {
      renderTable({ filters: { ...DEFAULT_FILTERS, q: 'alpha' } })
      expect(screen.getByText('1 of 3 jobs')).toBeInTheDocument()
    })

    it('groups thousands through the locale formatter', () => {
      const many = Array.from({ length: 1500 }, (_, i) => scored({ id: `x${i}`, title: `Job ${i}` }))
      renderTable({ data: runDetail({ scored: many }), locale: 'da' })
      expect(screen.getByText('1.500 job')).toBeInTheDocument()
    })

    it('changes the sort key without touching the direction', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'score', dir: 'asc' } })
      await userEvent.selectOptions(screen.getByLabelText(/sort by/i), 'company')
      expect(onSortChange).toHaveBeenCalledWith({ key: 'company', dir: 'asc' })
    })

    it('offers a key for every column plus one for your rating', () => {
      renderTable()
      const options = within(screen.getByLabelText(/sort by/i)).getAllByRole('option')
      expect(options.map((o) => o.getAttribute('value'))).toEqual([
        'score', 'title', 'company', 'portal', 'location', 'posted', 'mark',
      ])
    })

    it('flips the direction and renames the toggle so the change is announced', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'score', dir: 'desc' } })
      const toggle = screen.getByRole('button', { name: /descending/i })
      await userEvent.click(toggle)
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'asc' })
    })

    it('hides the reset while the sort is the default', () => {
      renderTable()
      expect(screen.queryByRole('button', { name: /reset sort/i })).not.toBeInTheDocument()
    })

    it('restores rating descending from the reset', async () => {
      const { onSortChange } = renderTable({ sort: { key: 'title', dir: 'asc' } })
      await userEvent.click(screen.getByRole('button', { name: /reset sort/i }))
      expect(onSortChange).toHaveBeenCalledWith(DEFAULT_SORT)
    })
  })

  describe('sort and filters are independent', () => {
    it('does not offer to reset the filters when only the sort has moved', () => {
      renderTable({ sort: { key: 'title', dir: 'asc' } })
      expect(screen.queryByRole('button', { name: /reset filters/i })).not.toBeInTheDocument()
    })

    it('resets the filters without reporting a sort change', async () => {
      const { onFiltersChange, onSortChange } = renderTable({
        filters: { ...DEFAULT_FILTERS, q: 'engineer' },
        sort: { key: 'title', dir: 'asc' },
      })
      await userEvent.click(screen.getByRole('button', { name: /reset filters/i }))
      expect(onFiltersChange).toHaveBeenCalledWith(DEFAULT_FILTERS)
      expect(onSortChange).not.toHaveBeenCalled()
    })

    it('resets the sort without reporting a filter change', async () => {
      const { onFiltersChange, onSortChange } = renderTable({
        filters: { ...DEFAULT_FILTERS, q: 'engineer' },
        sort: { key: 'title', dir: 'asc' },
      })
      await userEvent.click(screen.getByRole('button', { name: /reset sort/i }))
      expect(onSortChange).toHaveBeenCalledWith(DEFAULT_SORT)
      expect(onFiltersChange).not.toHaveBeenCalled()
    })
  })
})
