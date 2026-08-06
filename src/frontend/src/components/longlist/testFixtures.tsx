import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { vi } from 'vitest'
import type { RunDetail, ScoredEntry } from '../../api/types'
import { I18nProvider } from '../../i18n'
import type { Locale } from '../../i18n'
import { LonglistTable } from '../LonglistTable'
import { DEFAULT_FILTERS, type LonglistFilters } from './filterState'
import { DEFAULT_SORT, type LonglistSort } from './sortState'

// Shared by the sorting and filtering suites; imported only by tests, so it never reaches the bundle.

export function scored(over: Partial<ScoredEntry> & { id: string; title: string }): ScoredEntry {
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

export const ROWS: ScoredEntry[] = [
  scored({ id: 'a', title: 'Alpha Engineer', company: 'Zeta', score: 0.20, postedAt: '2026-07-01T00:00:00Z' }),
  scored({ id: 'b', title: 'Bravo Engineer', company: 'Acme', score: 0.90 }),
  scored({ id: 'c', title: 'Charlie Engineer', company: 'Mid', score: 0.50, postedAt: '2026-08-01T00:00:00Z' }),
]

export function runDetail(over: Partial<RunDetail> = {}): RunDetail {
  return {
    runId: 'run-1',
    startedAt: '2026-08-01T00:00:00Z',
    shortlist: [],
    marks: {},
    scored: ROWS,
    ...over,
  } as RunDetail
}

export function renderLonglist({
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
export function bodyTitles(): string[] {
  return screen.getAllByRole('row').slice(1) // drop the header row
    .map((r) => r.querySelector('td a')?.textContent ?? '')
    .filter(Boolean)
}

export function header(name: RegExp) {
  return screen.getByRole('columnheader', { name })
}

export function headerControl(name: RegExp) {
  return within(header(name)).getByRole('button')
}
