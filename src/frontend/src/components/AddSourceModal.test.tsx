import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AddSourceModal } from './AddSourceModal'
import * as client from '../api/client'
import type { SourceOverlap } from '../api/types'

vi.mock('../api/client')

const preview = (fetchedCount: number, overlap?: SourceOverlap) => ({
  test: {
    ok: fetchedCount > 0,
    fetchedCount,
    durationMs: 120,
    testedAt: new Date().toISOString(),
    samples: [],
    hitPageCap: false,
    possiblyCapped: false,
  },
  overlap,
})

function renderModal(props: Partial<Parameters<typeof AddSourceModal>[0]> = {}) {
  return render(
    <AddSourceModal
      onClose={() => {}}
      onCreated={() => {}}
      onOpenExisting={() => {}}
      {...props}
    />,
  )
}

async function pasteAndFind(url: string) {
  await userEvent.type(screen.getByPlaceholderText(/greenhouse/i), url)
  await userEvent.click(screen.getByRole('button', { name: /find it/i }))
}

describe('AddSourceModal', () => {
  beforeEach(() => vi.resetAllMocks())

  it('detects a pasted URL and creates the source', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({
      candidates: [{ kind: 'greenhouse', displayName: 'Monzo', summary: 'Greenhouse job board for Monzo — fetched automatically.' }],
    })
    vi.mocked(client.previewSource).mockResolvedValue(preview(12))
    vi.mocked(client.createSource).mockResolvedValue({ id: 10000 })
    const onCreated = vi.fn()

    renderModal({ onCreated })

    await pasteAndFind('https://boards.greenhouse.io/monzo')

    await waitFor(() => expect(screen.getByText(/Greenhouse job board for Monzo/i)).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: /add source/i }))

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(10000, 'Monzo'))
    expect(client.createSource).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'greenhouse', displayName: 'Monzo' }),
    )
  })

  // Recognising the address was never the point — the user wants to know it returns jobs, without
  // having to ask for a test first.
  it('fetches the jobs as soon as a candidate is recognised', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({
      candidates: [{ kind: 'oracle', displayName: 'Danskebank', summary: 'Oracle Recruiting Cloud careers site (CX_1001) — fetched automatically.' }],
    })
    vi.mocked(client.previewSource).mockResolvedValue(preview(140))

    renderModal()
    await pasteAndFind('https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/jobs')

    await waitFor(() => expect(screen.getByText(/found 140 jobs/i)).toBeInTheDocument())
    expect(client.previewSource).toHaveBeenCalledWith(expect.objectContaining({ kind: 'oracle' }))
  })

  it('names the source the user already has when the jobs are the same', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({
      candidates: [{ kind: 'oracle', displayName: 'Danskebank', summary: 'Oracle Recruiting Cloud careers site (CX_1001) — fetched automatically.' }],
    })
    vi.mocked(client.previewSource).mockResolvedValue(preview(140, {
      providerId: 44,
      displayName: 'Danske Bank (Oracle)',
      existingCount: 133,
      sharedCount: 133,
      ratio: 1,
      duplicate: true,
    }))
    const onOpenExisting = vi.fn()

    renderModal({ onOpenExisting })
    await pasteAndFind('https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/jobs')

    await waitFor(() => expect(screen.getByText(/you already have this source/i)).toBeInTheDocument())
    expect(screen.getByText(/already brings in 133 of these 140 jobs/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /open danske bank/i }))
    expect(onOpenExisting).toHaveBeenCalledWith(expect.objectContaining({ providerId: 44 }))

    // Adding it anyway stays available — the duplicate check informs, it doesn't block.
    expect(screen.getByRole('button', { name: /add anyway/i })).toBeEnabled()
  })

  it('reports a partial overlap without calling it a duplicate', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({
      candidates: [{ kind: 'rss', displayName: 'Example (feed)', summary: 'Looks like a job feed — fetched automatically.' }],
    })
    vi.mocked(client.previewSource).mockResolvedValue(preview(40, {
      providerId: 7,
      displayName: 'Jobindex',
      existingCount: 400,
      sharedCount: 18,
      ratio: 0.45,
      duplicate: false,
    }))

    renderModal()
    await pasteAndFind('https://example.com/jobs/feed')

    await waitFor(() => expect(screen.getByText(/18 of these 40 jobs also come from/i)).toBeInTheDocument())
    expect(screen.queryByText(/you already have this source/i)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /add source/i })).toBeInTheDocument()
  })

  it('offers manual import when nothing is detected', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({ candidates: [] })

    renderModal()
    await pasteAndFind('https://example.com/careers')

    await waitFor(() => expect(screen.getByRole('button', { name: /set up manual import/i })).toBeInTheDocument())
  })
})
