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

function stubDesktop(printToPdf: (name: string) => Promise<boolean>) {
  window.jobfinderDesktop = { quit: vi.fn(), printToPdf: vi.fn(printToPdf) }
  return window.jobfinderDesktop.printToPdf as ReturnType<typeof vi.fn>
}

afterEach(() => {
  delete window.jobfinderDesktop
})

describe('PrintListingButton', () => {
  it('mounts the print portal with the full ad text while the desktop save is in flight', async () => {
    let resolve!: (saved: boolean) => void
    const printToPdf = stubDesktop(() => new Promise<boolean>(r => { resolve = r }))
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

  it('prints the header block without a body for runs recorded before the ad text persisted', async () => {
    let resolve!: (saved: boolean) => void
    const printToPdf = stubDesktop(() => new Promise<boolean>(r => { resolve = r }))
    const legacy = { ...match, description: undefined }
    render(<PrintListingButton match={legacy} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() => expect(printToPdf).toHaveBeenCalled())
    expect(screen.getByText('Senior .NET Engineer')).toBeInTheDocument()
    expect(document.body.querySelector('.print-listing__body')).toBeNull()
    resolve(true)
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

  it('falls back to window.print() when an older shell lacks the channel', async () => {
    window.jobfinderDesktop = { quit: vi.fn() }
    const print = vi.fn()
    vi.stubGlobal('print', print)
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    await waitFor(() => expect(print).toHaveBeenCalled())
    vi.unstubAllGlobals()
  })

  it('shows the failure toast when the desktop save reports an error', async () => {
    stubDesktop(() => Promise.resolve(false))
    render(<PrintListingButton match={match} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save as PDF' }))

    expect(await screen.findByText('Could not save the PDF')).toBeInTheDocument()
    await userEvent.click(screen.getByText('Could not save the PDF'))
    expect(screen.queryByText('Could not save the PDF')).not.toBeInTheDocument()
  })
})

describe('suggestedPdfFileName', () => {
  it('is "{company} - {title}.pdf", dropping a missing company', () => {
    expect(suggestedPdfFileName({ company: 'Acme', title: 'Engineer' })).toBe('Acme - Engineer.pdf')
    expect(suggestedPdfFileName({ company: undefined, title: 'Engineer' })).toBe('Engineer.pdf')
  })
})
