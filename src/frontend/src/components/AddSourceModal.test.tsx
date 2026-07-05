import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AddSourceModal } from './AddSourceModal'
import * as client from '../api/client'

vi.mock('../api/client')

describe('AddSourceModal', () => {
  beforeEach(() => vi.resetAllMocks())

  it('detects a pasted URL and creates the source', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({
      candidates: [{ kind: 'greenhouse', displayName: 'Monzo', summary: 'Greenhouse job board for Monzo — fetched automatically.' }],
    })
    vi.mocked(client.createSource).mockResolvedValue({ id: 10000 })
    const onCreated = vi.fn()

    render(<AddSourceModal onClose={() => {}} onCreated={onCreated} />)

    await userEvent.type(screen.getByPlaceholderText(/greenhouse/i), 'https://boards.greenhouse.io/monzo')
    await userEvent.click(screen.getByRole('button', { name: /find it/i }))

    await waitFor(() => expect(screen.getByText(/Greenhouse job board for Monzo/i)).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: /add source/i }))

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(10000, 'Monzo'))
    expect(client.createSource).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'greenhouse', displayName: 'Monzo' }),
    )
  })

  it('offers manual import when nothing is detected', async () => {
    vi.mocked(client.detectSource).mockResolvedValue({ candidates: [] })

    render(<AddSourceModal onClose={() => {}} onCreated={() => {}} />)

    await userEvent.type(screen.getByPlaceholderText(/greenhouse/i), 'https://example.com/careers')
    await userEvent.click(screen.getByRole('button', { name: /find it/i }))

    await waitFor(() => expect(screen.getByRole('button', { name: /set up manual import/i })).toBeInTheDocument())
  })
})
