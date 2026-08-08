import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { RunDetail } from '../../api/types'
import { runDetail } from '../../components/longlist/testFixtures'
import { I18nProvider } from '../../i18n'
import type { TabKey } from './hash'
import { ViewMenu } from './ViewMenu'

const rawListing = (id: string) => ({ id, title: id, url: `https://example.test/${id}` })

function auditedRun(over: Partial<RunDetail> = {}): RunDetail {
  return runDetail({
    shortlist: [{ id: 'a' }, { id: 'b' }] as RunDetail['shortlist'],
    raw: [{ provider: 'p1', listings: [rawListing('r1'), rawListing('r2'), rawListing('r3')] }],
    dedupeMerges: [{ canonicalId: 'r1', mergedFromIds: ['r2'] }],
    dropped: [{ id: 'r3', title: 'r3', score: 0.1, reason: 'below_min_score', context: '' }],
    ...over,
  })
}

function renderMenu({ active = 'shortlist' as TabKey, data = auditedRun() } = {}) {
  const onChange = vi.fn()
  render(
    <I18nProvider locale="en">
      <ViewMenu active={active} onChange={onChange} data={data} />
    </I18nProvider>,
  )
  return { onChange }
}

describe('ViewMenu', () => {
  it('names the active view and its count on the closed trigger', () => {
    renderMenu()
    const trigger = screen.getByRole('button', { name: /Top jobs 2/ })
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('button', { name: /All fetched/ })).not.toBeInTheDocument()
  })

  it('lists all five views with counts when opened, audit views behind a separator', async () => {
    renderMenu()
    await userEvent.click(screen.getByRole('button', { name: /Top jobs/ }))
    const panel = screen.getByRole('group', { name: /result view/i })
    for (const name of [/Top jobs 2/, /All rated 3/, /All fetched 3/, /Duplicates 1/, /Removed 1/]) {
      expect(within(panel).getByRole('button', { name })).toBeEnabled()
    }
    expect(panel.querySelector('.view-menu__divider')).toBeInTheDocument()
  })

  it('reports the chosen view and closes', async () => {
    const { onChange } = renderMenu()
    await userEvent.click(screen.getByRole('button', { name: /Top jobs/ }))
    await userEvent.click(screen.getByRole('button', { name: /All rated/ }))
    expect(onChange).toHaveBeenCalledWith('longlist')
    expect(screen.getByRole('button', { name: /Top jobs 2/ })).toHaveAttribute('aria-expanded', 'false')
  })

  it('marks the active view in the list', async () => {
    renderMenu({ active: 'dedupe' })
    await userEvent.click(screen.getByRole('button', { name: /Duplicates 1/ }))
    const panel = screen.getByRole('group', { name: /result view/i })
    const active = within(panel).getByRole('button', { name: /Duplicates/ })
    expect(active).toHaveAttribute('aria-current', 'true')
  })

  it('shows an audit view on the trigger while it is active', () => {
    renderMenu({ active: 'raw' })
    expect(screen.getByRole('button', { name: /All fetched 3/ })).toBeInTheDocument()
  })

  it('disables a view the run never recorded', async () => {
    const { onChange } = renderMenu({ data: auditedRun({ dropped: undefined, scored: undefined }) })
    await userEvent.click(screen.getByRole('button', { name: /Top jobs/ }))
    const removed = screen.getByRole('button', { name: /Removed/ })
    expect(removed).toBeDisabled()
    expect(removed).toHaveAttribute('title', 'Not recorded for this search.')
    expect(screen.getByRole('button', { name: /All rated/ })).toBeDisabled()
    await userEvent.click(removed).catch(() => {})
    expect(onChange).not.toHaveBeenCalled()
  })

  it('is dismissed by Escape with focus handed back to the trigger', async () => {
    renderMenu()
    const trigger = screen.getByRole('button', { name: /Top jobs/ })
    await userEvent.click(trigger)
    await userEvent.keyboard('{Escape}')
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    expect(trigger).toHaveFocus()
  })
})
