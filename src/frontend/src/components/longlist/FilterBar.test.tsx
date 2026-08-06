import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DEFAULT_FILTERS } from './filterState'
import { renderLonglist, runDetail, scored } from './testFixtures'

vi.mock('../../api/client', () => ({
  setMark: vi.fn(() => Promise.resolve({ success: true })),
  setMarkStatus: vi.fn(() => Promise.resolve({ success: true })),
}))

// 12 sources, descending by listing count: portal-0 has the most, portal-11 the fewest.
const MANY_SOURCES = Array.from({ length: 12 }, (_, p) =>
  Array.from({ length: 12 - p }, (_, i) =>
    scored({ id: `p${p}-${i}`, title: `Job ${p}-${i}`, portal: `portal-${p}`, portalDisplayName: `Portal ${p}` }),
  ),
).flat()

function sourceChips(): string[] {
  const group = screen.getByRole('group', { name: /source/i })
  return within(group).getAllByRole('button').map((b) => b.textContent ?? '')
}

function portalChips(): string[] {
  return sourceChips().filter((c) => c.startsWith('Portal'))
}

describe('source filter', () => {
  it('shows only the busiest sources until asked for the rest', () => {
    renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    expect(portalChips()).toHaveLength(8)
    expect(sourceChips().some((c) => /show all \(12\)/i.test(c))).toBe(true)
    // Busiest first: Portal 0 has 12 listings.
    expect(sourceChips()[0]).toMatch(/Portal 0\s*12/)
  })

  it('reveals every source and collapses again', async () => {
    renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    await userEvent.click(screen.getByRole('button', { name: /show all \(12\)/i }))
    expect(portalChips()).toHaveLength(12)

    await userEvent.click(screen.getByRole('button', { name: /show fewer/i }))
    expect(portalChips()).toHaveLength(8)
  })

  it('keeps a selected source visible even when it falls outside the visible slice', () => {
    // portal-11 is the smallest, so it sits well past the 8-chip cut. Hiding it would leave the row
    // count unexplained.
    renderLonglist({
      data: runDetail({ scored: MANY_SOURCES }),
      filters: { ...DEFAULT_FILTERS, portals: ['portal-11'] },
    })
    expect(portalChips()).toHaveLength(9)
    const group = screen.getByRole('group', { name: /source/i })
    expect(within(group).getByRole('button', { name: /Portal 11/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('omits the toggle when every source already fits', () => {
    renderLonglist()
    expect(screen.queryByRole('button', { name: /show all/i })).not.toBeInTheDocument()
  })
})

describe('rating window', () => {
  it('exposes both bounds as labelled sliders', () => {
    renderLonglist()
    expect(screen.getByRole('slider', { name: /minimum rating/i })).toBeInTheDocument()
    expect(screen.getByRole('slider', { name: /maximum rating/i })).toBeInTheDocument()
  })

  it('pins the minimum at the maximum rather than inverting the window', () => {
    const { onFiltersChange } = renderLonglist({
      filters: { ...DEFAULT_FILTERS, scoreMin: 0.2, scoreMax: 0.5 },
    })
    fireEvent.change(screen.getByRole('slider', { name: /minimum rating/i }), { target: { value: '0.9' } })
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ scoreMin: 0.5, scoreMax: 0.5 }))
  })

  it('pins the maximum at the minimum rather than inverting the window', () => {
    const { onFiltersChange } = renderLonglist({
      filters: { ...DEFAULT_FILTERS, scoreMin: 0.4, scoreMax: 0.8 },
    })
    fireEvent.change(screen.getByRole('slider', { name: /maximum rating/i }), { target: { value: '0.1' } })
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ scoreMin: 0.4, scoreMax: 0.4 }))
  })

  it('puts the minimum thumb on top only when the pair is pinned high, where only it can move', () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, scoreMin: 1, scoreMax: 1 } })
    expect(document.querySelector('.range-dual')).toHaveClass('range-dual--min-on-top')
  })

  it('leaves the maximum thumb on top when the pair is pinned low', () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, scoreMin: 0, scoreMax: 0 } })
    expect(document.querySelector('.range-dual')).not.toHaveClass('range-dual--min-on-top')
  })
})
