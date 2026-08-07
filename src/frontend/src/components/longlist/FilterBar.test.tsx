import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DEFAULT_FILTERS } from './filterState'
import { renderLonglist, runDetail, scored } from './testFixtures'

vi.mock('../../api/client', () => ({
  setMark: vi.fn(() => Promise.resolve({ success: true })),
  setMarkStatus: vi.fn(() => Promise.resolve({ success: true })),
}))

// 15 sources, descending by listing count: portal-0 has the most, portal-14 the fewest. Above the
// threshold where the panel grows its own search box.
const MANY_SOURCES = Array.from({ length: 15 }, (_, p) =>
  Array.from({ length: 15 - p }, (_, i) =>
    scored({ id: `p${p}-${i}`, title: `Job ${p}-${i}`, portal: `portal-${p}`, portalDisplayName: `Portal ${p}` }),
  ),
).flat()

const trigger = (name: RegExp) => screen.getByRole('button', { name })
const panel = (name: RegExp) => screen.getByRole('group', { name })

describe('collapsed filter groups', () => {
  it('shows one trigger per group and no options until opened', () => {
    renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    for (const name of [/^source/, /^posted/, /^rating/, /^your rating/]) {
      expect(trigger(name)).toHaveAttribute('aria-expanded', 'false')
    }
    expect(screen.queryByRole('checkbox', { name: /Portal 0/ })).not.toBeInTheDocument()
  })

  it('opens a group on click and exposes every source', async () => {
    renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    await userEvent.click(trigger(/^source/))
    expect(trigger(/^source/)).toHaveAttribute('aria-expanded', 'true')
    expect(within(panel(/^source$/)).getAllByRole('checkbox')).toHaveLength(15)
  })

  it('opening one group closes the previous one', async () => {
    renderLonglist()
    await userEvent.click(trigger(/^posted/))
    expect(trigger(/^posted/)).toHaveAttribute('aria-expanded', 'true')

    await userEvent.click(trigger(/^your rating/))
    expect(trigger(/^posted/)).toHaveAttribute('aria-expanded', 'false')
    expect(trigger(/^your rating/)).toHaveAttribute('aria-expanded', 'true')
  })

  it('closes on Escape and hands focus back to the trigger', async () => {
    renderLonglist()
    await userEvent.click(trigger(/^posted/))
    await userEvent.keyboard('{Escape}')
    expect(trigger(/^posted/)).toHaveAttribute('aria-expanded', 'false')
    expect(trigger(/^posted/)).toHaveFocus()
  })

  it('stays open while several sources are ticked', async () => {
    const { onFiltersChange } = renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    await userEvent.click(trigger(/^source/))
    await userEvent.click(within(panel(/^source$/)).getByRole('checkbox', { name: /Portal 0/ }))
    expect(trigger(/^source/)).toHaveAttribute('aria-expanded', 'true')
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ portals: ['portal-0'] }))
  })

  it('narrows a long source list by search', async () => {
    renderLonglist({ data: runDetail({ scored: MANY_SOURCES }) })
    await userEvent.click(trigger(/^source/))
    await userEvent.type(within(panel(/^source$/)).getByRole('searchbox'), 'Portal 11')
    expect(within(panel(/^source$/)).getAllByRole('checkbox')).toHaveLength(1)
  })
})

describe('trigger state', () => {
  it('counts the selected sources', () => {
    renderLonglist({
      data: runDetail({ scored: MANY_SOURCES }),
      filters: { ...DEFAULT_FILTERS, portals: ['portal-0', 'portal-3'] },
    })
    expect(trigger(/^source 2/)).toBeInTheDocument()
  })

  it('names the chosen posting age and rating window', () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, posted: '7d', scoreMin: 0.2, scoreMax: 0.8 } })
    expect(trigger(/^posted 7d/)).toBeInTheDocument()
    expect(trigger(/^rating 0\.20–0\.80/)).toBeInTheDocument()
  })

  it('names the chosen mark', () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, mark: 'good' } })
    expect(trigger(/^your rating good/)).toBeInTheDocument()
  })

  it('says nothing extra when a group is unfiltered', () => {
    renderLonglist()
    expect(trigger(/^posted$/)).toBeInTheDocument()
  })

  it('clears just its own group', async () => {
    const { onFiltersChange } = renderLonglist({
      filters: { ...DEFAULT_FILTERS, posted: '7d', q: 'engineer' },
    })
    await userEvent.click(trigger(/^posted 7d/))
    await userEvent.click(within(panel(/^posted$/)).getByRole('button', { name: /clear/i }))
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ posted: 'any', q: 'engineer' }))
  })

  it('offers no clear while the group is unfiltered', async () => {
    renderLonglist()
    await userEvent.click(trigger(/^posted/))
    expect(within(panel(/^posted$/)).queryByRole('button', { name: /clear/i })).not.toBeInTheDocument()
  })
})

describe('rating window', () => {
  it('exposes both bounds as labelled sliders', async () => {
    renderLonglist()
    await userEvent.click(trigger(/^rating/))
    expect(screen.getByRole('slider', { name: /minimum rating/i })).toBeInTheDocument()
    expect(screen.getByRole('slider', { name: /maximum rating/i })).toBeInTheDocument()
  })

  it('pins the minimum at the maximum rather than inverting the window', async () => {
    const { onFiltersChange } = renderLonglist({
      filters: { ...DEFAULT_FILTERS, scoreMin: 0.2, scoreMax: 0.5 },
    })
    await userEvent.click(trigger(/^rating/))
    fireEvent.change(screen.getByRole('slider', { name: /minimum rating/i }), { target: { value: '0.9' } })
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ scoreMin: 0.5, scoreMax: 0.5 }))
  })

  it('pins the maximum at the minimum rather than inverting the window', async () => {
    const { onFiltersChange } = renderLonglist({
      filters: { ...DEFAULT_FILTERS, scoreMin: 0.4, scoreMax: 0.8 },
    })
    await userEvent.click(trigger(/^rating/))
    fireEvent.change(screen.getByRole('slider', { name: /maximum rating/i }), { target: { value: '0.1' } })
    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ scoreMin: 0.4, scoreMax: 0.4 }))
  })

  it('puts the minimum thumb on top only when the pair is pinned high, where only it can move', async () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, scoreMin: 1, scoreMax: 1 } })
    await userEvent.click(trigger(/^rating/))
    expect(document.querySelector('.range-dual')).toHaveClass('range-dual--min-on-top')
  })

  it('leaves the maximum thumb on top when the pair is pinned low', async () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, scoreMin: 0, scoreMax: 0 } })
    await userEvent.click(trigger(/^rating/))
    expect(document.querySelector('.range-dual')).not.toHaveClass('range-dual--min-on-top')
  })

  it('previews the score distribution, raising only the occupied bins', async () => {
    renderLonglist()
    await userEvent.click(trigger(/^rating/))
    const bars = document.querySelectorAll<HTMLElement>('.score-hist__bar')
    expect(bars).toHaveLength(20)
    // Fixture scores 0.20 / 0.50 / 0.90 land in bins 4, 10 and 18; every other bin is flat.
    const raised = [...bars].map((b, i) => (b.style.height !== '0%' ? i : -1)).filter((i) => i >= 0)
    expect(raised).toEqual([4, 10, 18])
  })

  it('marks only the bins inside the window as selected', async () => {
    renderLonglist({ filters: { ...DEFAULT_FILTERS, scoreMin: 0.5 } })
    await userEvent.click(trigger(/^rating/))
    const bars = document.querySelectorAll<HTMLElement>('.score-hist__bar')
    expect(bars[4]).not.toHaveClass('score-hist__bar--in')   // 0.20 — cut away
    expect(bars[10]).toHaveClass('score-hist__bar--in')      // 0.50 — inside
    expect(bars[18]).toHaveClass('score-hist__bar--in')      // 0.90 — inside
  })
})

describe('retired top-jobs toggle', () => {
  it('is gone: the Top jobs view owns that subset now', () => {
    renderLonglist()
    expect(screen.queryByRole('button', { name: /top jobs only/i })).not.toBeInTheDocument()
  })
})
