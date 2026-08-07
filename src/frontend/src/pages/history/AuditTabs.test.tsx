import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { RunDetail } from '../../api/types'
import { runDetail } from '../../components/longlist/testFixtures'
import { I18nProvider } from '../../i18n'
import { AuditTabs } from './AuditTabs'
import type { TabKey } from './hash'

const rawListing = (id: string) => ({ id, title: id, url: `https://example.test/${id}` })

function auditedRun(over: Partial<RunDetail> = {}): RunDetail {
  return runDetail({
    raw: [{ provider: 'p1', listings: [rawListing('r1'), rawListing('r2'), rawListing('r3')] }],
    dedupeMerges: [{ canonicalId: 'r1', mergedFromIds: ['r2'] }],
    dropped: [{ id: 'r3', title: 'r3', score: 0.1, reason: 'below_min_score', context: '' }],
    ...over,
  })
}

function renderTabs({ active = 'shortlist' as TabKey, data = auditedRun() } = {}) {
  const onChange = vi.fn()
  render(
    <I18nProvider locale="en">
      <AuditTabs active={active} onChange={onChange} data={data} />
    </I18nProvider>,
  )
  return { onChange }
}

const trigger = () => screen.getByRole('button', { name: /^show/ })

describe('AuditTabs', () => {
  it('collapses the three audit views to one closed trigger', () => {
    renderTabs()
    expect(trigger()).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('button', { name: /all fetched/ })).not.toBeInTheDocument()
  })

  it('lists every audit view with its count when opened', async () => {
    renderTabs()
    await userEvent.click(trigger())
    const panel = screen.getByRole('group', { name: 'show' })
    expect(within(panel).getByRole('button', { name: /all fetched 3/ })).toBeEnabled()
    expect(within(panel).getByRole('button', { name: /duplicates 1/ })).toBeEnabled()
    expect(within(panel).getByRole('button', { name: /removed 1/ })).toBeEnabled()
  })

  it('reports the chosen view and closes', async () => {
    const { onChange } = renderTabs()
    await userEvent.click(trigger())
    await userEvent.click(screen.getByRole('button', { name: /duplicates/ }))
    expect(onChange).toHaveBeenCalledWith('dedupe')
    expect(trigger()).toHaveAttribute('aria-expanded', 'false')
  })

  it('disables a view the run never recorded', async () => {
    const { onChange } = renderTabs({ data: auditedRun({ dropped: undefined }) })
    await userEvent.click(trigger())
    const removed = screen.getByRole('button', { name: /removed/ })
    expect(removed).toBeDisabled()
    expect(removed).toHaveAttribute('title', 'Not recorded for this search.')
    expect(onChange).not.toHaveBeenCalled()
  })

  it('names the active audit view on the trigger, so the collapsed state stays readable', () => {
    renderTabs({ active: 'raw' })
    expect(screen.getByRole('button', { name: /show all fetched/ })).toBeInTheDocument()
  })
})
