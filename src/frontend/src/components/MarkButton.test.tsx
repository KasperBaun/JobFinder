import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import { I18nProvider } from '../i18n'
import { MarkButton } from './MarkButton'

vi.mock('../api/client', () => ({
  setMark: vi.fn(() => Promise.resolve({ success: true })),
}))

function renderMark(current?: 'good' | 'bad') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <I18nProvider locale="en">
      <QueryClientProvider client={qc}>
        <MarkButton runId="run-1" listingId="a" current={current} />
      </QueryClientProvider>
    </I18nProvider>,
  )
}

beforeEach(() => {
  vi.mocked(client.setMark).mockClear()
})

describe('MarkButton', () => {
  it('offers good and bad as two separate controls', () => {
    renderMark()
    expect(screen.getByRole('button', { name: /good match/i })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: /not a match/i })).toHaveAttribute('aria-pressed', 'false')
  })

  it('records bad in a single click, without passing through good', async () => {
    renderMark()
    await userEvent.click(screen.getByRole('button', { name: /not a match/i }))
    expect(client.setMark).toHaveBeenCalledTimes(1)
    expect(client.setMark).toHaveBeenCalledWith({ runId: 'run-1', listingId: 'a', mark: 'bad', reason: null })
  })

  it('clears the mark when the pressed control is clicked again', async () => {
    renderMark('good')
    const good = screen.getByRole('button', { name: /marked as a good match/i })
    expect(good).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(good)
    expect(client.setMark).toHaveBeenCalledWith({ runId: 'run-1', listingId: 'a', mark: null, reason: null })
  })

  it('flips good to bad directly', async () => {
    renderMark('good')
    await userEvent.click(screen.getByRole('button', { name: /not a match/i }))
    expect(client.setMark).toHaveBeenCalledTimes(1)
    expect(client.setMark).toHaveBeenCalledWith({ runId: 'run-1', listingId: 'a', mark: 'bad', reason: null })
  })
})
