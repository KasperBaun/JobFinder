import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DEFAULT_FILTERS } from './longlist/filterState'
import { DEFAULT_SORT } from './longlist/sortState'
import {
  bodyTitles,
  header,
  headerControl,
  renderLonglist,
  runDetail,
  scored,
} from './longlist/testFixtures'

vi.mock('../api/client', () => ({
  setMark: vi.fn(() => Promise.resolve({ success: true })),
  setMarkStatus: vi.fn(() => Promise.resolve({ success: true })),
}))

describe('LonglistTable', () => {
  it('renders nothing but a notice when the run recorded no ratings', () => {
    renderLonglist({ data: runDetail({ scored: undefined }) })
    expect(screen.getByText(/no ratings recorded/i)).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('orders by rating descending by default', () => {
    renderLonglist()
    expect(bodyTitles()).toEqual(['Bravo Engineer', 'Charlie Engineer', 'Alpha Engineer'])
  })

  describe('sortable headers', () => {
    it('marks exactly one column as sorted, and it is the active one', () => {
      renderLonglist()
      const sorted = screen.getAllByRole('columnheader').filter((h) => h.hasAttribute('aria-sort'))
      expect(sorted).toHaveLength(1)
      expect(sorted[0]).toHaveAttribute('aria-sort', 'descending')
      expect(sorted[0]).toHaveTextContent(/rating/i)
    })

    it('exposes every sortable column through a focusable control', () => {
      renderLonglist()
      for (const name of [/title/i, /company/i, /source/i, /location/i, /posted/i, /^rating/i, /your rating/i]) {
        expect(headerControl(name)).toBeInstanceOf(HTMLButtonElement)
      }
    })

    it('is reachable by keyboard alone', async () => {
      const { onSortChange } = renderLonglist()
      const control = headerControl(/title/i)
      control.focus()
      expect(control).toHaveFocus()
      await userEvent.keyboard('{Enter}')
      expect(onSortChange).toHaveBeenCalledWith({ key: 'title', dir: 'asc' })

      await userEvent.keyboard(' ')
      expect(onSortChange).toHaveBeenCalledTimes(2)
    })

    it('starts a text column ascending and the rating column descending', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'title', dir: 'asc' } })
      await userEvent.click(headerControl(/company/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'company', dir: 'asc' })

      await userEvent.click(headerControl(/^rating/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'desc' })
    })

    it('reverses the direction when the active column is clicked again', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'score', dir: 'desc' } })
      await userEvent.click(headerControl(/^rating/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'asc' })
    })

    it('never asks for an unsorted third state', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'title', dir: 'desc' } })
      await userEvent.click(headerControl(/title/i))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'title', dir: 'asc' })
    })

    it('applies the sort it is given', () => {
      // Deliberately a key whose order differs from the rating-descending default, so passing
      // proves the sort ran rather than that nothing happened.
      renderLonglist({ sort: { key: 'title', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Bravo Engineer', 'Charlie Engineer'])
    })

    it('puts listings with no posting date last', () => {
      renderLonglist({ sort: { key: 'posted', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Charlie Engineer', 'Bravo Engineer'])
    })

    it('sorts by your rating, which lives on the run rather than on the listing', () => {
      // Proves the marks recorded against the run reach the comparator: 'a' has the lowest score,
      // so rating-descending would put it last.
      const data = runDetail({ marks: { a: 'good', b: 'bad' } })
      renderLonglist({ data, sort: { key: 'mark', dir: 'asc' } })
      expect(bodyTitles()).toEqual(['Alpha Engineer', 'Charlie Engineer', 'Bravo Engineer'])
      expect(header(/your rating/i)).toHaveAttribute('aria-sort', 'ascending')
    })
  })

  describe('sort bar', () => {
    it('states the visible count and total', () => {
      renderLonglist()
      expect(screen.getByText('3 jobs')).toBeInTheDocument()
    })

    it('reports a filtered subset against the full run', () => {
      renderLonglist({ filters: { ...DEFAULT_FILTERS, q: 'alpha' } })
      expect(screen.getByText('1 of 3 jobs')).toBeInTheDocument()
    })

    it('groups thousands through the locale formatter', () => {
      const many = Array.from({ length: 1500 }, (_, i) => scored({ id: `x${i}`, title: `Job ${i}` }))
      renderLonglist({ data: runDetail({ scored: many }), locale: 'da' })
      expect(screen.getByText('1.500 job')).toBeInTheDocument()
    })

    it('changes the sort key without touching the direction', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'score', dir: 'asc' } })
      await userEvent.selectOptions(screen.getByLabelText(/sort by/i), 'company')
      expect(onSortChange).toHaveBeenCalledWith({ key: 'company', dir: 'asc' })
    })

    it('offers a key for every column plus one for your rating', () => {
      renderLonglist()
      const options = within(screen.getByLabelText(/sort by/i)).getAllByRole('option')
      expect(options.map((o) => o.getAttribute('value'))).toEqual([
        'score', 'title', 'company', 'portal', 'location', 'posted', 'mark',
      ])
    })

    it('flips the direction and renames the toggle so the change is announced', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'score', dir: 'desc' } })
      await userEvent.click(screen.getByRole('button', { name: /descending/i }))
      expect(onSortChange).toHaveBeenCalledWith({ key: 'score', dir: 'asc' })
    })

    it('hides the reset while the sort is the default', () => {
      renderLonglist()
      expect(screen.queryByRole('button', { name: /reset sort/i })).not.toBeInTheDocument()
    })

    it('restores rating descending from the reset', async () => {
      const { onSortChange } = renderLonglist({ sort: { key: 'title', dir: 'asc' } })
      await userEvent.click(screen.getByRole('button', { name: /reset sort/i }))
      expect(onSortChange).toHaveBeenCalledWith(DEFAULT_SORT)
    })
  })

  describe('pagination', () => {
    // Score strictly descending, so the page order equals the index order and slices are provable.
    const many = (count: number) =>
      runDetail({
        scored: Array.from({ length: count }, (_, i) =>
          scored({ id: `x${i}`, title: `Job ${String(i).padStart(3, '0')}`, score: 1 - i * 0.0001 }),
        ),
      })

    it('renders only the current page of rows', () => {
      renderLonglist({ data: many(120), size: 50 })
      expect(bodyTitles()).toHaveLength(50)
      expect(screen.getByText('page 1 of 3')).toBeInTheDocument()
    })

    it('shows the requested page, not the first', () => {
      renderLonglist({ data: many(120), size: 50, page: 2 })
      const titles = bodyTitles()
      expect(titles).toHaveLength(50)
      expect(titles[0]).toBe('Job 050')
    })

    it('clamps a page beyond the end to the last page instead of an empty table', () => {
      renderLonglist({ data: many(120), size: 50, page: 99 })
      expect(bodyTitles()).toHaveLength(20)
      expect(screen.getByText('page 3 of 3')).toBeInTheDocument()
    })

    it('steps one page in either direction', async () => {
      const { onPageChange } = renderLonglist({ data: many(120), size: 50, page: 2 })
      await userEvent.click(screen.getByRole('button', { name: /next page/i }))
      expect(onPageChange).toHaveBeenCalledWith(3)
      await userEvent.click(screen.getByRole('button', { name: /previous page/i }))
      expect(onPageChange).toHaveBeenCalledWith(1)
    })

    it('disables stepping past either end', () => {
      renderLonglist({ data: many(120), size: 50 })
      expect(screen.getByRole('button', { name: /previous page/i })).toBeDisabled()
      expect(screen.getByRole('button', { name: /next page/i })).toBeEnabled()
    })

    it('offers the fixed page sizes and reports a choice', async () => {
      const { onSizeChange } = renderLonglist({ data: many(120) })
      const select = screen.getByLabelText(/per page/i)
      expect(within(select).getAllByRole('option').map((o) => o.getAttribute('value')))
        .toEqual(['50', '100', '150', '200', '250', '300'])
      await userEvent.selectOptions(select, '150')
      expect(onSizeChange).toHaveBeenCalledWith(150)
    })
  })

  describe('sort and filters are independent', () => {
    it('does not offer to reset the filters when only the sort has moved', () => {
      renderLonglist({ sort: { key: 'title', dir: 'asc' } })
      expect(screen.queryByRole('button', { name: /reset filters/i })).not.toBeInTheDocument()
    })

    it('resets the filters without reporting a sort change', async () => {
      const { onFiltersChange, onSortChange } = renderLonglist({
        filters: { ...DEFAULT_FILTERS, q: 'engineer' },
        sort: { key: 'title', dir: 'asc' },
      })
      await userEvent.click(screen.getByRole('button', { name: /reset filters/i }))
      expect(onFiltersChange).toHaveBeenCalledWith(DEFAULT_FILTERS)
      expect(onSortChange).not.toHaveBeenCalled()
    })

    it('resets the sort without reporting a filter change', async () => {
      const { onFiltersChange, onSortChange } = renderLonglist({
        filters: { ...DEFAULT_FILTERS, q: 'engineer' },
        sort: { key: 'title', dir: 'asc' },
      })
      await userEvent.click(screen.getByRole('button', { name: /reset sort/i }))
      expect(onSortChange).toHaveBeenCalledWith(DEFAULT_SORT)
      expect(onFiltersChange).not.toHaveBeenCalled()
    })
  })
})
