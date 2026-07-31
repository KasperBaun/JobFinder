import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ListingMatch } from '../api/types'
import { suggestedPdfFileName } from '../hooks/usePrintListing'
import { PrintListingButton } from './PrintListingButton'

const match: ListingMatch = {
  id: 'l1',
  portal: 'mine',
  portalDisplayName: 'My imports',
  title: 'Senior .NET Engineer',
  company: 'Acme',
  location: 'Copenhagen',
  remoteMode: 'remote',
  url: 'https://acme.com/jobs/1',
  postedAt: '2026-07-01T09:00:00Z',
  score: 0.82,
  reasoning: '',
  primaryStackHits: ['C#'],
  secondaryStackHits: [],
  description: 'Full ad text.\nSecond paragraph.',
}

function stubDesktop(bridge: Partial<NonNullable<Window['jobfinderDesktop']>>) {
  window.jobfinderDesktop = { quit: vi.fn(), ...bridge }
  return window.jobfinderDesktop
}

afterEach(() => {
  delete window.jobfinderDesktop
})

describe('PrintListingButton', () => {
  it('sends the posting URL to the desktop source capture — the PDF is the ad, not the summary', async () => {
    const printSourceToPdf = vi.fn(() => Promise.resolve(true))
    stubDesktop({ printSourceToPdf })
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() =>
      expect(printSourceToPdf).toHaveBeenCalledWith('https://acme.com/jobs/1', 'Acme - Senior .NET Engineer.pdf'),
    )
  })

  it('offers the source capture even when the run has no persisted ad text', async () => {
    const printSourceToPdf = vi.fn(() => Promise.resolve(true))
    stubDesktop({ printSourceToPdf })
    const legacy = { ...match, description: undefined }
    render(<PrintListingButton match={legacy} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() => expect(printSourceToPdf).toHaveBeenCalledWith('https://acme.com/jobs/1', expect.any(String)))
  })

  it('hides the button when neither the source capture nor persisted ad text can make a useful PDF', () => {
    const legacy = { ...match, description: undefined }
    render(<PrintListingButton match={legacy} />)

    expect(screen.queryByRole('button', { name: 'Save as PDF' })).not.toBeInTheDocument()
  })

  it('mounts the print portal with the full ad text while an older shell captures the page', async () => {
    let resolve!: (saved: boolean) => void
    const printToPdf = vi.fn(() => new Promise<boolean>(r => { resolve = r }))
    stubDesktop({ printToPdf })
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() => expect(printToPdf).toHaveBeenCalledWith('Acme - Senior .NET Engineer.pdf'))
    expect(screen.getByText('Senior .NET Engineer')).toBeInTheDocument()
    expect(screen.getByText('Acme · Copenhagen')).toBeInTheDocument()
    expect(screen.getByText('My imports')).toBeInTheDocument()
    expect(screen.getByText('https://acme.com/jobs/1')).toBeInTheDocument()
    expect(screen.getByText(/Full ad text\./)).toBeInTheDocument()

    resolve(true)
    await waitFor(() => expect(screen.queryByText('Senior .NET Engineer')).not.toBeInTheDocument())
  })

  it('falls back to window.print() outside the desktop shell', async () => {
    const print = vi.fn()
    vi.stubGlobal('print', print)
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() => expect(print).toHaveBeenCalled())
    await waitFor(() => expect(screen.queryByText(/Full ad text/)).not.toBeInTheDocument())
    vi.unstubAllGlobals()
  })

  it('shows the failure toast when the source capture reports an error', async () => {
    stubDesktop({ printSourceToPdf: vi.fn(() => Promise.resolve(false)) })
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    expect(await screen.findByText('Could not save the PDF')).toBeInTheDocument()
    await userEvent.click(screen.getByText('Could not save the PDF'))
    expect(screen.queryByText('Could not save the PDF')).not.toBeInTheDocument()
  })

  it('shows the failure toast when an older shell reports a failed save', async () => {
    stubDesktop({ printToPdf: vi.fn(() => Promise.resolve(false)) })
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    expect(await screen.findByText('Could not save the PDF')).toBeInTheDocument()
  })
})

describe('suggestedPdfFileName', () => {
  it('is "{company} - {title}.pdf", dropping a missing company', () => {
    expect(suggestedPdfFileName({ company: 'Acme', title: 'Engineer' })).toBe('Acme - Engineer.pdf')
    expect(suggestedPdfFileName({ company: undefined, title: 'Engineer' })).toBe('Engineer.pdf')
  })
})
