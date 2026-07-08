import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { setMarkStatus } from '../api/client'
import type { ApplicationStatus, RunDetail } from '../api/types'

interface Props {
  runId: string
  listingId: string
  current?: ApplicationStatus
  compact?: boolean
}

export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  applied: 'Applied',
  interview: 'Interview',
  offer: 'Offer',
  rejected: 'Rejected',
  'no-response': 'No response',
}

const STATUS_ORDER: ApplicationStatus[] = ['applied', 'interview', 'offer', 'rejected', 'no-response']

const TOOLTIP = 'Track what happened after applying. Interviews and offers teach the AI what a strong fit looks like.'

// The application-status control next to a MarkButton. Independent of the
// good/bad mark — either can be set or cleared without affecting the other.
export function StatusSelect({ runId, listingId, current, compact }: Props) {
  const [optimistic, setOptimistic] = useState<ApplicationStatus | undefined>(current)
  const [error, setError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  useEffect(() => {
    setOptimistic(current)
  }, [current])

  const mutation = useMutation({
    mutationFn: async (status: ApplicationStatus | null) => {
      const res = await setMarkStatus({ runId, listingId, status })
      if (!res.success) throw new Error(res.error ?? 'Status update failed')
      return status
    },
    onSuccess: (status) => {
      setError(null)
      queryClient.setQueryData<RunDetail | undefined>(['run', runId], (prev) => {
        if (!prev) return prev
        const markStatuses = { ...prev.markStatuses }
        if (status === null) {
          delete markStatuses[listingId]
        } else {
          markStatuses[listingId] = status
        }
        return { ...prev, markStatuses }
      })
      void queryClient.invalidateQueries({ queryKey: ['applications'] })
    },
    onError: (err) => {
      setOptimistic(current)
      setError(err instanceof Error ? err.message : String(err))
      console.error('Status update failed:', err)
    },
  })

  function handleChange(value: string) {
    const next = value === '' ? null : (value as ApplicationStatus)
    setOptimistic(next ?? undefined)
    setError(null)
    mutation.mutate(next)
  }

  const cls = [
    'status-select',
    optimistic ? `status-select--${optimistic}` : '',
    compact ? 'status-select--compact' : '',
  ].filter(Boolean).join(' ')

  return (
    <span className="status-select-wrap">
      <select
        className={cls}
        value={optimistic ?? ''}
        onChange={(e) => handleChange(e.target.value)}
        disabled={mutation.isPending}
        aria-label={TOOLTIP}
        data-tooltip={TOOLTIP}
      >
        <option value="">{optimistic ? 'Clear status' : 'Status…'}</option>
        {STATUS_ORDER.map((s) => (
          <option key={s} value={s}>{STATUS_LABELS[s]}</option>
        ))}
      </select>
      {error && <span className="mark-button__error">{error}</span>}
    </span>
  )
}
